using System;
using UnityEngine;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Manages the player's currency.
    /// Notifies listeners via OnCoinsChanged when the balance updates.
    /// </summary>
    public sealed class PlayerWallet : MonoBehaviour
    {
        /// <summary>Current coin balance.</summary>
        public int Coins { get; private set; }

        /// <summary>Fired whenever the coin balance changes.</summary>
        public event Action<int> OnCoinsChanged;

        /// <summary>
        /// Adds a positive amount of coins to the wallet.
        /// </summary>
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            OnCoinsChanged?.Invoke(Coins);
        }
    }
}
