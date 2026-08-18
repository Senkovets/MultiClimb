using UnityEngine;

namespace _Project.CodeBase.Movement
{
    [CreateAssetMenu(menuName = "MultiClimb/Locomotion Config")]
    public class LocomotionConfig : ScriptableObject
    {
        [Header("Speed")]
        [Tooltip("ВАЖНО: в KCC-процессоре KinematicSpeed выстави равной " +
                 "SprintSpeed. Ходьба получается уменьшением ввода.")]
        public float SprintSpeed = 7.5f;
 
        public float WalkSpeed = 4.5f;
 
        [Range(0.2f, 1f)]
        public float AimSpeedMultiplier = 0.55f;
 
        [Header("Turning, deg/sec")]
        [Tooltip("Поворот корпуса к прицелу при обычной ходьбе")]
        public float WalkTurnSpeed = 900f;
 
        [Tooltip("Поворот корпуса по движению во время спринта")]
        public float SprintTurnSpeed = 540f;
 
        [Tooltip("Возврат к прицелу после спринта. " +
                 "700 = разворот на 180 за ~0.26 сек")]
        public float ReturnTurnSpeed = 700f;
 
        [Tooltip("При прицеливании — самый быстрый")]
        public float AimTurnSpeed = 1200f;
 
        [Tooltip("Сколько секунд действует ReturnTurnSpeed после спринта")]
        public float ReturnDuration = 0.35f;
 
        [Header("Sprint")]
        public float SprintMinInput = 0.5f;
        public bool BlockFireWhileSprinting = true;
 
        [Header("Roll")]
        [Tooltip("Сила рывка. Идёт через AddExternalImpulse, " +
                 "как Shove — затухает трением KCC.")]
        public float RollImpulse = 14f;
 
        [Tooltip("Сколько держится состояние Rolling: " +
                 "поворот заблокирован, стрелять нельзя")]
        public float RollDuration = 0.4f;
 
        public float RollCooldown = 1.2f;
    }
}