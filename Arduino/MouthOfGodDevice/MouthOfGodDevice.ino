/*
  Mouth of God で使う触感デバイス（充填 / 吸引）の制御スケッチ。
  DRV8835 + Arduino <-> Unity の、1 行ずつの通信です。Unity 側は Assets/Scripts/Device/ にあります。
  約束（プロトコル）の詳しい説明は docs/device-protocol.md です。
  もとは MealBeBack のスケッチ（https://github.com/Fialuxe/MealBeBack/issues/42）です。

  Unity -> Arduino : "(<種類>,<値>)\n"
    'f' = 中の量を <値>% まで「増やす」 (既に <値> 以上なら何もしない)
    's' = 中の量を <値>% まで「減らす」 (既に <値> 以下なら何もしない)
    'c' = モーターを回さず、いまの量を <値>% と覚え直す (実物とのずれを直す用)
    'i' = すぐ止める (<値> は無視)
    'r' = 原点出し。押し出す側 (100%) の端まで回して、100% と記録する (<値> は無視)
    't' = 動作確認。押す・引くを、それぞれ 50% ぶん、<値> 回くり返す (押すほうから始める)。
          <値> は 1-20。範囲外は無視する。
          端にいて動けない向きの動きは飛ばす (起動直後は 100% なので、最初の「押す」は飛ばす)。
          途中で別の指令 (f / s / c / i / r) が届いたら、やめる。
    <値> : 0-100 の整数。範囲外の f / s / c は無視する。

  Arduino -> Unity : "(<動作>,<量>)\n"   (SEND_INTERVAL_MS ごと)
    <動作> : 1 = 駆動中 / 0 = 停止中
    <量>   : いまの内容量 0-100 (駆動中は、動き出す前の値のまま)
  Arduino -> Unity : "(k,1)\n"          (確定ボタンを押した瞬間に 1 回)
  Arduino -> Unity : "(h,MOG1)\n"       (「ここに実機がいる」という合図。起動時と HELLO_INTERVAL_MS ごと)
    Unity は COM ポートを順に開いて、この合図が届いたポートを実機として使う。
    MOG = Mouth Of God、1 = 約束の版。約束を変えたら、Unity の DeviceProtocol.HelloValue と一緒に上げる。

  知らない行・壊れた行は、読み捨てる (Unity 側も同じ)。

  センサーが無いので、内容量は、モーターを回した時間から数えた記録。
  起動した直後に、原点出しをして、100% と決める (HOME_ON_BOOT)。
  原点出しは、押し出す側の端にぶつかるまで回す。歯車への負担を減らすため、
    ・記録から、端までの時間を計算して、その分だけ回す
    ・端にぶつけたまま回す時間は、HOMING_MARGIN_MS だけにする
  起動した直後は位置が分からないので、BOOT_ASSUMED_PERCENT から端までの時間と、余分に回す時間を回す。
  実物の量が違うときは、Unity から "(c,<実量>)" で記録を直すか、"(r,0)" で原点出しをする。

  Aチャンネル(AIN1/AIN2) = fillモーター(押す)
  Bチャンネル(BIN1/BIN2) = suckモーター(引く)
  MODE=LOW固定(PHASE/ENABLE) : IN1=PHASE(方向), IN2=ENABLE(有効/無効)

  ※ 押し引きが逆に組まれた個体 (Arduino Micro 等) は、REVERSE_DIRECTION を 1 にして書き込む。
     プロトコルと Unity 側は共通。

  実装方針 (最小):
    ・目標値との差 (%) ぶんだけモーターを回す時間を決めて回す (loop は止めない)。
    ・時間に達したら currentInside を目標値にして止まる。
    ・動いている途中で別の指令・停止が来たら、そこまでに動いた分だけ currentInside に反映して、
      新しい指令へ切り替える。
    ・それでも、動作中に指令を重ねると実物とずれやすい。Unity は、止まったこと
      (動作 = 0) を確かめてから、次を送る。
*/

// ==== ピン定義 ====
const int AIN1 = 5;   // fillモーター PHASE
const int AIN2 = 6;   // fillモーター ENABLE
const int BIN1 = 9;   // suckモーター PHASE
const int BIN2 = 10;  // suckモーター ENABLE
const int MODE = 4;   // PHASE/ENABLEモード固定

// 確定ボタン。ボタンの片側をこのピン、もう片側を GND につなぐ。
// 内蔵プルアップを使うので、ボタンが付いていなくても、押したことにはならない。
const int CONFIRM_BUTTON_PIN = 2;
const unsigned long BUTTON_DEBOUNCE_MS = 30;

// ==== 向き ====
// 0 = 標準。1 = 押し引きが逆に組まれた個体 (driveMotor の向きを反転する)。
#define REVERSE_DIRECTION 0

// ==== 通信設定 ====
const long BAUD_RATE = 9600;               // Unity の DeviceActuator の Baud Rate と同じにする
const unsigned long SEND_INTERVAL_MS = 50; // 状態送信の周期
const unsigned long HELLO_INTERVAL_MS = 1000; // 実機の合図の周期

// ==== 1%あたりの駆動時間(実測して調整する) ====
const unsigned long MS_PER_PERCENT = 10;

// ==== 原点出し ====
// 1 = 起動した直後に、原点出しをする (起動直後の位置は分からないので、全行程ぶん回す)。
#define HOME_ON_BOOT 1

// 起動した直後に、内容量がこの値だと見て、端までの時間を決める。
// 0 = いちばん遠い所にいると見て、全行程ぶん回す (確実。ただし、すでに端にいると、端にぶつけたまま全行程ぶん回す)。
// 普段、実機を置く位置が決まっているなら、その値(例: 80)にすると、回す時間が短くなる。
// 実物が、この値より遠い所にいると、端に届かない。
const int BOOT_ASSUMED_PERCENT = 0;

// 記録から計算した時間に足す、余分に回す時間(ms)。端にぶつけたまま回す時間になる。
// 短いほど歯車への負担が小さい。ただし、MS_PER_PERCENT の誤差より短いと、端に届かない。
// 端に届かないときは、MS_PER_PERCENT を実測し直すか、この値を少しずつ増やす。
const unsigned long HOMING_MARGIN_MS = 100;

// ==== 動作確認 ====
const int TEST_STEP_PERCENT = 50;    // (t,N) で、押す・引くそれぞれが動かす量 (%)
const int TEST_MAX_CYCLES = 20;      // (t,N) の N の上限

// ==== 状態管理 ====
enum MotorState { MOTOR_STOPPED, MOTOR_FILLING, MOTOR_SUCKING };

MotorState motorState = MOTOR_STOPPED;

int currentInside = 100;             // 0-100。動作の完了時と停止時に更新する
int actionTargetInside = 0;          // 進行中の動作が終わったあとの currentInside
unsigned long actionStartMillis = 0; // 進行中の動作の開始時刻
unsigned long actionDurationMs = 0;  // 進行中の動作の駆動時間

int testStepsLeft = 0;                 // 動作確認で、まだ行っていない動きの数 (押す・引くで 1 往復 = 2)
MotorState testNextDir = MOTOR_FILLING; // 動作確認の、次の動きの向き

char lineBuf[16];   // 受信行バッファ("(f,100)" が入れば十分)
int lineLen = 0;
unsigned long lastSendAt = 0;
unsigned long lastHelloAt = 0;

// 確定ボタン
int buttonStableState = HIGH;        // デバウンス済みの状態 (HIGH = 離している)
int buttonLastReading = HIGH;
unsigned long buttonChangedAt = 0;

void setup() {
  Serial.begin(BAUD_RATE);

  pinMode(AIN1, OUTPUT);
  pinMode(AIN2, OUTPUT);
  pinMode(BIN1, OUTPUT);
  pinMode(BIN2, OUTPUT);
  pinMode(MODE, OUTPUT);
  pinMode(CONFIRM_BUTTON_PIN, INPUT_PULLUP);
  digitalWrite(MODE, LOW);

  stopMotors();
  sendHello(); // 起動したことを、すぐ知らせる

#if HOME_ON_BOOT
  currentInside = BOOT_ASSUMED_PERCENT;
  startHoming();
#endif
}

void loop() {
  readSerial();
  updateMotion();
  updateConfirmButton();

  if (millis() - lastSendAt >= SEND_INTERVAL_MS) {
    lastSendAt = millis();
    sendStatus();
  }

  if (millis() - lastHelloAt >= HELLO_INTERVAL_MS) {
    sendHello();
  }
}

// ============================================================
// シリアル受信(非ブロッキング)。'\n' で 1 行確定
// ============================================================
void readSerial() {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();

    if (c == '\n') {
      lineBuf[lineLen] = '\0';
      handleLine(lineBuf);
      lineLen = 0;
    } else if (c == '\r') {
      // 無視
    } else if (lineLen < (int)sizeof(lineBuf) - 1) {
      lineBuf[lineLen++] = c;
    } else {
      lineLen = 0; // 想定外に長い行はまるごと捨てて継続
    }
  }
}

// 期待フォーマット: "(f,80)" 。'(' は飛ばし、',' の後ろを数値として読む
void handleLine(const char *line) {
  const char *p = line;
  if (*p == '(') p++;

  char cmd = *p;
  const char *comma = strchr(p, ',');
  if (comma == NULL) return;          // ',' が無い行は無視

  int value = atoi(comma + 1);        // "80)" -> 80

  if (cmd != 't') testStepsLeft = 0;  // 動作確認の途中で別の指令が来たら、動作確認をやめる

  switch (cmd) {
    case 'i':
      settleProgress();
      stopMotors();
      motorState = MOTOR_STOPPED;
      break;

    case 'r':
      startHoming();
      break;

    case 't':
      if (value < 1 || value > TEST_MAX_CYCLES) return;
      startTest(value);
      break;

    case 'c':
      if (value < 0 || value > 100) return;
      stopMotors();
      motorState = MOTOR_STOPPED;
      currentInside = value;          // モーターは回さず現在値だけ書き換える
      break;

    case 'f':
      if (value < 0 || value > 100) return;
      moveToward(MOTOR_FILLING, value);
      break;

    case 's':
      if (value < 0 || value > 100) return;
      moveToward(MOTOR_SUCKING, value);
      break;

    default:
      return; // 未定義コマンドは無視
  }
}

// ============================================================
// currentInside を target に近づける。
// dir が示す向き (FILLING=増やす / SUCKING=減らす) にしか動かさない。
// ============================================================
void moveToward(MotorState dir, int target) {
  settleProgress(); // 動いている途中なら、そこまでの分を反映してから、新しい向きを決める

  int delta = target - currentInside;

  // 指定の向きと逆、または既に到達済みなら何もしない
  if (dir == MOTOR_FILLING && delta <= 0) { stopMotors(); motorState = MOTOR_STOPPED; return; }
  if (dir == MOTOR_SUCKING && delta >= 0) { stopMotors(); motorState = MOTOR_STOPPED; return; }

  motorState = dir;
  actionTargetInside = target;
  actionStartMillis = millis();
  actionDurationMs = (unsigned long)abs(delta) * MS_PER_PERCENT;

  driveMotor(dir);
}

// 原点出し。押し出す側の端まで回して、終わったら 100% と記録する。
// 記録から端までの時間を計算し、HOMING_MARGIN_MS を足した分だけ回す。
void startHoming() {
  settleProgress();

  unsigned long distanceMs = (unsigned long)(100 - currentInside) * MS_PER_PERCENT;

  motorState = MOTOR_FILLING;
  actionTargetInside = 100;
  actionStartMillis = millis();
  actionDurationMs = distanceMs + HOMING_MARGIN_MS;

  driveMotor(MOTOR_FILLING);
}

// 動作確認。押す・引くを、それぞれ TEST_STEP_PERCENT ぶん、cycles 回くり返す。押すほうから始める。
void startTest(int cycles) {
  settleProgress();
  testStepsLeft = cycles * 2;
  testNextDir = MOTOR_FILLING;
  startNextTestStep();
}

// 動作確認の、次の動きを始める。端にいて動けない向きの動きは飛ばす。
// 動きが残っていなければ、止まる。
void startNextTestStep() {
  while (testStepsLeft > 0) {
    MotorState dir = testNextDir;
    int step = (dir == MOTOR_FILLING) ? TEST_STEP_PERCENT : -TEST_STEP_PERCENT;
    int target = constrain(currentInside + step, 0, 100);

    testNextDir = (dir == MOTOR_FILLING) ? MOTOR_SUCKING : MOTOR_FILLING;
    testStepsLeft--;

    int delta = target - currentInside;
    if (delta == 0) continue;

    motorState = dir;
    actionTargetInside = target;
    actionStartMillis = millis();
    actionDurationMs = (unsigned long)abs(delta) * MS_PER_PERCENT;
    driveMotor(dir);
    return;
  }

  stopMotors();
  motorState = MOTOR_STOPPED;
}

// 動いている途中で止める・切り替えるとき、そこまでに動いた分を currentInside に反映する。
// 止まっているときは何もしない。
void settleProgress() {
  if (motorState == MOTOR_STOPPED) return;

  unsigned long elapsed = millis() - actionStartMillis;
  if (elapsed > actionDurationMs) elapsed = actionDurationMs;
  int movedPercent = (int)(elapsed / MS_PER_PERCENT);

  if (motorState == MOTOR_FILLING) currentInside += movedPercent;
  else                             currentInside -= movedPercent;
  currentInside = constrain(currentInside, 0, 100);
}

// 駆動時間に到達したら目標値へ更新して停止する
void updateMotion() {
  if (motorState == MOTOR_STOPPED) return;

  if (millis() - actionStartMillis >= actionDurationMs) {
    currentInside = constrain(actionTargetInside, 0, 100);

    if (testStepsLeft > 0) {
      startNextTestStep(); // 動作確認の途中。止まらずに、次の動きへ進む
      return;
    }

    stopMotors();
    motorState = MOTOR_STOPPED;
  }
}

void driveMotor(MotorState state) {
#if REVERSE_DIRECTION
  state = (state == MOTOR_FILLING) ? MOTOR_SUCKING : MOTOR_FILLING;
#endif
  if (state == MOTOR_FILLING) {
    digitalWrite(AIN1, LOW);
    digitalWrite(AIN2, HIGH);
    digitalWrite(BIN1, HIGH);
    digitalWrite(BIN2, LOW);
  } else if (state == MOTOR_SUCKING) {
    digitalWrite(AIN1, HIGH);
    digitalWrite(AIN2, LOW);
    digitalWrite(BIN1, LOW);
    digitalWrite(BIN2, HIGH);
  }
}

void stopMotors() {
  digitalWrite(AIN1, LOW);
  digitalWrite(AIN2, LOW);
  digitalWrite(BIN1, LOW);
  digitalWrite(BIN2, LOW);
}

// ============================================================
// 確定ボタン。押した瞬間に 1 回だけ "(k,1)" を送る (押し続けても 1 回)
// ============================================================
void updateConfirmButton() {
  int reading = digitalRead(CONFIRM_BUTTON_PIN);

  if (reading != buttonLastReading) {
    buttonLastReading = reading;
    buttonChangedAt = millis();
  }

  if (millis() - buttonChangedAt >= BUTTON_DEBOUNCE_MS && reading != buttonStableState) {
    buttonStableState = reading;
    if (buttonStableState == LOW) {
      Serial.print("(k,1)\n");
    }
  }
}

// 「ここに実機がいる」という合図。Unity が、実機の COM ポートを見つけるために使う。
void sendHello() {
  lastHelloAt = millis();
  Serial.print("(h,MOG1)\n");
}

void sendStatus() {
  Serial.print('(');
  Serial.print(motorState == MOTOR_STOPPED ? 0 : 1);
  Serial.print(',');
  Serial.print(currentInside);
  Serial.print(')');
  Serial.print('\n');
}
