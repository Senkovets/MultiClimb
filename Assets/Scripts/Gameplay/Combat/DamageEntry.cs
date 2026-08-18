using Fusion;
using UnityEngine;

namespace Gameplay.Combat
{
    public struct DamageEntry : INetworkStruct
    {
        public float Damage;        // полный урон до разделения
        public float ToArmor;       // сколько ушло в броню
        public float ToHealth;      // сколько ушло в HP
        public Vector3 Point;       // точка попадания
        public byte Flags;          // бит 0 = крит
 
        public bool IsCrit => (Flags & 1) != 0;
    }
}