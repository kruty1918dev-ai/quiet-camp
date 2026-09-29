using System;
using System.Text.RegularExpressions;

namespace Kruty1918.UIActions.API
{
    public readonly struct UiActionId : IEquatable<UiActionId>
    {
        private const string Pattern = "^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$";
        private static readonly Regex CanonicalRegex = new(Pattern, RegexOptions.Compiled);

        public UiActionId(string value)
        {
            if (!IsValid(value))
                throw new ArgumentException($"Invalid UI action id '{value}'.", nameof(value));

            Value = value;
        }

        public string Value { get; }

        public static bool IsValid(string value)
            => !string.IsNullOrWhiteSpace(value) && CanonicalRegex.IsMatch(value);

        public bool Equals(UiActionId other)
            => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is UiActionId other && Equals(other);

        public override int GetHashCode()
            => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

        public override string ToString() => Value;

        public static bool operator ==(UiActionId left, UiActionId right)
            => left.Equals(right);

        public static bool operator !=(UiActionId left, UiActionId right)
            => !left.Equals(right);
    }
}
