using System;
using System.Collections.Generic;
using MouthOfGod.Core;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// チュートリアルが始まってから、体験者がしたことの記録。待ちが、先回りした操作も数えるために使う。
    /// たとえば「刺して」と言う前に刺していたら、刺すのを待つ待ちは、すぐ終わる。
    /// </summary>
    /// <remarks>
    /// - 記録するのは、刺した・かけらの付いた先を口に含んだ・食感が変わった・決定した（食感あり）・食べ物が生まれた、の 5 つ。
    /// - 待ちは、まだ使われていない記録を数える。待ちが終わると、その種類の記録は、そこまで使ったことにする（同じ操作で 2 回進まない）。
    /// - <see cref="TutorialRunner"/> が、チュートリアル 1 つごとに作り、終わったら <see cref="Dispose"/> する。前のチュートリアルの操作は数えない。
    /// </remarks>
    public sealed class TutorialHistory : IDisposable
    {
        readonly List<Entry> _entries = new List<Entry>();
        readonly Dictionary<TutorialEvent, int> _usedUpTo = new Dictionary<TutorialEvent, int>();
        int _sequence;
        IFork _fork;
        IDevice _device;
        IFoodSpawner _spawner;
        bool _disposed;

        readonly struct Entry
        {
            public Entry(int sequence, TutorialEvent kind, TextureId texture, Spawned spawned)
            {
                Sequence = sequence;
                Kind = kind;
                Texture = texture;
                Spawned = spawned;
            }

            public int Sequence { get; }
            public TutorialEvent Kind { get; }
            public TextureId Texture { get; }
            public Spawned Spawned { get; }
        }

        public TutorialHistory()
        {
            Forks.WhenReady(BindFork);
            Devices.WhenReady(BindDevice);
            FoodSpawners.WhenReady(BindSpawner);
        }

        /// <summary>kind の、まだ使われていない記録の数。</summary>
        public int Pending(TutorialEvent kind)
        {
            int used = UsedUpTo(kind);
            int count = 0;
            foreach (Entry e in _entries)
                if (e.Kind == kind && e.Sequence > used) count++;
            return count;
        }

        /// <summary>kind の記録を、今までの分すべて使ったことにする。</summary>
        public void UseAll(TutorialEvent kind) => _usedUpTo[kind] = _sequence;

        // まだ使われていない、最初の決定。見つかれば、そこまで使ったことにする。
        internal bool TakeConfirm(out TextureId texture)
        {
            foreach (Entry e in _entries)
            {
                if (e.Kind != TutorialEvent.Confirmed || e.Sequence <= UsedUpTo(e.Kind)) continue;
                _usedUpTo[e.Kind] = e.Sequence;
                texture = e.Texture;
                return true;
            }
            texture = TextureId.None;
            return false;
        }

        // まだ使われていない、最初に生まれた食べ物（only を指定すれば、その食べ物だけ）。見つかれば、そこまで使ったことにする。
        internal bool TakeSpawn(FoodId only, out Spawned spawned)
        {
            foreach (Entry e in _entries)
            {
                if (e.Kind != TutorialEvent.Spawned || e.Sequence <= UsedUpTo(e.Kind)) continue;
                if (!only.IsEmpty && e.Spawned.Food != only) continue;
                _usedUpTo[e.Kind] = e.Sequence;
                spawned = e.Spawned;
                return true;
            }
            spawned = default;
            return false;
        }

        int UsedUpTo(TutorialEvent kind) => _usedUpTo.TryGetValue(kind, out int used) ? used : 0;

        // テストからも記録を足す。
        internal void Record(TutorialEvent kind, TextureId texture = default, Spawned spawned = default)
        {
            if (_disposed) return;
            _sequence++;
            _entries.Add(new Entry(_sequence, kind, texture, spawned));
        }

        void BindFork(IFork fork)
        {
            if (_disposed) return;
            _fork = fork;
            _fork.Pierced += OnPierced;
            _fork.MouthEntered += OnMouthEntered;
        }

        void BindDevice(IDevice device)
        {
            if (_disposed) return;
            _device = device;
            _device.TextureChanged += OnTextureChanged;
            _device.Confirmed += OnConfirmed;
        }

        void BindSpawner(IFoodSpawner spawner)
        {
            if (_disposed) return;
            _spawner = spawner;
            _spawner.Spawned += OnSpawned;
        }

        void OnPierced() => Record(TutorialEvent.Pierced);

        void OnMouthEntered()
        {
            if (_fork != null && _fork.IsLoaded) Record(TutorialEvent.MouthEntered); // かけらなしで口に入れても、数えない
        }

        void OnTextureChanged(TextureId texture) => Record(TutorialEvent.TextureChanged, texture);

        void OnConfirmed(TextureId texture)
        {
            if (!texture.IsEmpty) Record(TutorialEvent.Confirmed, texture); // 噛む前の決定は、数えない
        }

        void OnSpawned(Spawned spawned) => Record(TutorialEvent.Spawned, default, spawned);

        /// <summary>記録をやめ、聞くのをやめる。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Forks.CancelWhenReady(BindFork);
            Devices.CancelWhenReady(BindDevice);
            FoodSpawners.CancelWhenReady(BindSpawner);
            if (_fork != null)
            {
                _fork.Pierced -= OnPierced;
                _fork.MouthEntered -= OnMouthEntered;
            }
            if (_device != null)
            {
                _device.TextureChanged -= OnTextureChanged;
                _device.Confirmed -= OnConfirmed;
            }
            if (_spawner != null) _spawner.Spawned -= OnSpawned;
            _fork = null;
            _device = null;
            _spawner = null;
        }
    }

    /// <summary><see cref="TutorialHistory"/> が記録すること。</summary>
    public enum TutorialEvent
    {
        /// <summary>雲を刺した。</summary>
        Pierced,
        /// <summary>かけらの付いた先を、口に含んだ。</summary>
        MouthEntered,
        /// <summary>食感が変わった（噛み始めの最初の食感も含む）。</summary>
        TextureChanged,
        /// <summary>食感を選んで決定した。</summary>
        Confirmed,
        /// <summary>食べ物が生まれた。</summary>
        Spawned,
    }
}
