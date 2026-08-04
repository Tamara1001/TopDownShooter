using System;
using UnityEngine;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Gestiona la moneda del jugador.
    /// Notifica a los oyentes a través de OnCoinsChanged cuando se actualiza el saldo.
    /// </summary>
    public sealed class PlayerWallet : MonoBehaviour
    {
        /// <summary>Saldo de monedas actual.</summary>
        public int Coins { get; private set; }

        /// <summary>Se activa cada vez que cambia el saldo de monedas.</summary>
        public event Action<int> OnCoinsChanged;

        /// <summary>
        /// Añade una cantidad positiva de monedas al monedero.
        /// </summary>
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            OnCoinsChanged?.Invoke(Coins);
        }
    }
}
