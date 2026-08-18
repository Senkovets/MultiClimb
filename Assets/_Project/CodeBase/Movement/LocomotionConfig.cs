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
        public float WalkTurnSpeed = 720f;
 
        [Tooltip("Поворот корпуса по движению во время спринта")]
        public float SprintTurnSpeed = 260f;
 
        [Tooltip("Возврат к прицелу после спринта. " +
                 "700 = разворот на 180 за ~0.26 сек")]
        public float ReturnTurnSpeed = 500f;
 
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
        
        [Header("Inertia")]
        [Tooltip("Сколько секунд набирается полная скорость. " +
                 "0.15 отзывчиво, 0.35 тяжело, 0.5 уже танк.")]
        public float AccelerationTime = 0.25f;
 
        [Tooltip("Сколько секунд до полной остановки. " +
                 "Обычно быстрее разгона — иначе персонаж скользит.")]
        public float DecelerationTime = 0.15f;
 
        [Tooltip("Насколько медленнее меняется направление на бегу. " +
                 "0.4 = разворот на бегу вчетверо инертнее чем шагом. " +
                 "Это и есть 'нельзя резко свернуть когда разогнался'.")]
        [Range(0.1f, 1f)]
        public float SprintTurnInertia = 0.4f;
        
        [Tooltip("Множитель скорости при развороте на 180 на бегу. " +
                 "0.5 = теряешь половину темпа. 1 = штрафа нет.")]
        [Range(0.3f, 1f)]
        public float MinTurnSpeedPenalty = 0.5f;
    }
}