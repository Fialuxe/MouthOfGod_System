using System;

namespace MouthOfGod.Core
{
    /// <summary>食感の論理 ID です。文字列を包んだ値です。</summary>
    /// <remarks>
    /// <see langword="null"/> と空文字は、どちらも空 ID の <see cref="None"/> として扱います。
    /// 大文字と小文字は区別します。
    /// </remarks>
    public readonly struct TextureId : IEquatable<TextureId>
    {
        public static readonly TextureId None = default;

        readonly string _value;

        public TextureId(string value)
        {
            _value = string.IsNullOrEmpty(value) ? string.Empty : value;
        }

        public string Value => _value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(_value);

        public bool Equals(TextureId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is TextureId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(TextureId left, TextureId right) => left.Equals(right);

        public static bool operator !=(TextureId left, TextureId right) => !left.Equals(right);
    }


    /// <summary>山・海・草原・村といったフィールドの ID です。文字列を包んだ値です。</summary>
    /// <remarks>
    /// <see langword="null"/> と空文字は、どちらも空 ID の <see cref="None"/> として扱います。
    /// 大文字と小文字は区別します。
    /// </remarks>
    public readonly struct FieldId : IEquatable<FieldId>
    {
        public static readonly FieldId None = default;

        readonly string _value;

        public FieldId(string value)
        {
            _value = string.IsNullOrEmpty(value) ? string.Empty : value;
        }

        public string Value => _value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(_value);

        public bool Equals(FieldId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is FieldId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(FieldId left, FieldId right) => left.Equals(right);

        public static bool operator !=(FieldId left, FieldId right) => !left.Equals(right);
    }


    /// <summary>いまの場面の ID です。文字列を包んだ値です。</summary>
    /// <remarks>
    /// 場面は、チュートリアルの名前・本編・エンディングなどです。
    /// <see langword="null"/> と空文字は、どちらも空 ID の <see cref="None"/> として扱います。
    /// 大文字と小文字は区別します。
    /// </remarks>
    public readonly struct PhaseId : IEquatable<PhaseId>
    {
        public static readonly PhaseId None = default;

        readonly string _value;

        public PhaseId(string value)
        {
            _value = string.IsNullOrEmpty(value) ? string.Empty : value;
        }

        public string Value => _value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(_value);

        public bool Equals(PhaseId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is PhaseId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(PhaseId left, PhaseId right) => left.Equals(right);

        public static bool operator !=(PhaseId left, PhaseId right) => !left.Equals(right);
    }
}
