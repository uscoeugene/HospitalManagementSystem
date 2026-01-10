using System;

namespace HMS.API.Domain.ValueObjects
{
    public sealed class Money : IEquatable<Money>
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public Money(decimal amount, string currency = "USD")
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative");
            if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency must be provided", nameof(currency));

            Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
            Currency = currency.ToUpperInvariant();
        }

        public Money Add(Money other)
        {
            EnsureSameCurrency(other);
            return new Money(Amount + other.Amount, Currency);
        }

        public Money Subtract(Money other)
        {
            EnsureSameCurrency(other);
            var result = Amount - other.Amount;
            if (result < 0) throw new InvalidOperationException("Resulting amount cannot be negative");
            return new Money(result, Currency);
        }

        private void EnsureSameCurrency(Money other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Currency mismatch");
        }

        public override bool Equals(object? obj) => Equals(obj as Money);

        public bool Equals(Money? other) => other is not null && Amount == other.Amount && Currency == other.Currency;

        public override int GetHashCode() => HashCode.Combine(Amount, Currency);

        public override string ToString() => $"{Currency} {Amount:N2}";

        public static Money Zero(string currency = "USD") => new Money(0m, currency);
    }
}