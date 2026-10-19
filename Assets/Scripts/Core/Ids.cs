using System;

namespace MouthOfGod.Core
{
    /// <summary>食感の論理 ID。文字列を包んだ値。null と空文字は、どちらも空 ID（TextureId.None）。大文字小文字は区別する。</summary>
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

    /// <summary>食べ物の ID。文字列を包んだ値。null と空文字は、どちらも空 ID（FoodId.None）。大文字小文字は区別する。</summary>
    public readonly struct FoodId : IEquatable<FoodId>
    {
        public static readonly FoodId None = default;

        readonly string _value;

        public FoodId(string value)
        {
            _value = string.IsNullOrEmpty(value) ? string.Empty : value;
        }

        public string Value => _value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(_value);

        public bool Equals(FoodId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is FoodId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(FoodId left, FoodId right) => left.Equals(right);

        public static bool operator !=(FoodId left, FoodId right) => !left.Equals(right);
    }

    /// <summary>フィールド（山・海・草原・村）の ID。文字列を包んだ値。null と空文字は、どちらも空 ID（FieldId.None）。大文字小文字は区別する。</summary>
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

    /// <summary>Tutorial から Village や UI への指示の ID。文字列を包んだ値。null と空文字は、どちらも空 ID（CueId.None）。大文字小文字は区別する。</summary>
    public readonly struct CueId : IEquatable<CueId>
    {
        public static readonly CueId None = default;

        readonly string _value;

        public CueId(string value)
        {
            _value = string.IsNullOrEmpty(value) ? string.Empty : value;
        }

        public string Value => _value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(_value);

        public bool Equals(CueId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CueId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(CueId left, CueId right) => left.Equals(right);

        public static bool operator !=(CueId left, CueId right) => !left.Equals(right);
    }

    /// <summary>いまの場面（チュートリアルの名前、本編、エンディングなど）の ID。文字列を包んだ値。null と空文字は、どちらも空 ID（PhaseId.None）。大文字小文字は区別する。</summary>
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
