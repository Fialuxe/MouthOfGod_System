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
    <値> : 0-100 の整数。範囲外の f / s / c は無視する。

  Arduino -> Unity : "(<動作>,<量>)\n"   (SEND_INTERVAL_MS ごと)
    <動作> : 1 = 駆動中 / 0 = 停止中
    <量>   : いまの内容量 0-100 (駆動中は、動き出す前の値のまま)
  Arduino -> Unity : "(k,1)\n"          (確定ボタンを押した瞬間に 1 回)

  知らない行・壊れた行は、読み捨てる (Unity 側も同じ)。

  起動直後は「デバイスは 100% 充填済み」と覚えている (currentInside = 100)。
  実物の量が違うときは、Unity から "(c,<実量>)" を送って合わせる。

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

// ==== 1%あたりの駆動時間(実測して調整する) ====
const unsigned long MS_PER_PERCENT = 10;

// ==== 状態管理 ====
enum MotorState { MOTOR_STOPPED, MOTOR_FILLING, MOTOR_SUCKING };

MotorState motorState = MOTOR_STOPPED;

int currentInside = 100;             // 0-100。動作の完了時と停止時に更新する
int actionTargetInside = 0;          // 進行中の動作が終わったあとの currentInside
unsigned long actionStartMillis = 0; // 進行中の動作の開始時刻
unsigned long actionDurationMs = 0;  // 進行中の動作の駆動時間

char lineBuf[16];   // 受信行バッファ("(f,100)" が入れば十分)
int lineLen = 0;
unsigned long lastSendAt = 0;

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
}

void loop() {
  readSerial();
  updateMotion();
  updateConfirmButton();

  if (millis() - lastSendAt >= SEND_INTERVAL_MS) {
    lastSendAt = millis();
    sendStatus();
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

  switch (cmd) {
    case 'i':
      settleProgress();
      stopMotors();
      motorState = MOTOR_STOPPED;
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

void sendStatus() {
  Serial.print('(');
  Serial.print(motorState == MOTOR_STOPPED ? 0 : 1);
  Serial.print(',');
  Serial.print(currentInside);
  Serial.print(')');
  Serial.print('\n');
}
