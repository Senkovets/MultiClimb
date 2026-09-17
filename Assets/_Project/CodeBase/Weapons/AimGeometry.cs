using UnityEngine;
 
namespace _Project.CodeBase.Weapons
{
    public static class AimGeometry
    {
        /// <summary>
        /// Yaw корпуса, при котором ствол попадает на линию прицела.
        ///
        /// Без поправки корпус смотрит точно на цель, но ствол
        /// смещён вбок и пуля уходит параллельно. Поправка
        /// доворачивает корпус на компенсирующий угол.
        /// </summary>
        public static float GetCompensatedBodyYaw(
            Vector3 playerPos, Vector3 aimPoint, float lateralOffset)
        {
            Vector3 toAim = aimPoint - playerPos;
            toAim.y = 0f;
 
            if (toAim.sqrMagnitude < 0.0001f)
                return 0f;
 
            float baseYaw = Mathf.Atan2(toAim.x, toAim.z) * Mathf.Rad2Deg;
 
            float distance = toAim.magnitude;
            float absOffset = Mathf.Abs(lateralOffset);
 
            // Вблизи поправка не имеет смысла: asin уходит за 1 (NaN),
            // а угол стремится к 90 градусам. Как у Duckov — отключаем.
            if (distance < absOffset + 0.25f)
                return baseYaw;
 
            float correction = Mathf.Asin(lateralOffset / distance) * Mathf.Rad2Deg;
 
            // Минус: ствол справа — корпус доворачиваем влево
            return baseYaw - correction;
        }
 
        /// <summary>
        /// Позиция ствола, вычисленная из yaw корпуса и константы.
        ///
        /// НЕ читает transform: живой gunMuzzle лежит под ModelRoot,
        /// его позиция зависит от сглаженного поворота и разойдётся
        /// между сервером и клиентом.
        /// </summary>
        public static Vector3 GetMuzzlePosition(
            Vector3 playerPos, float bodyYaw, Vector3 localOffset)
        {
            Quaternion rot = Quaternion.Euler(0f, bodyYaw, 0f);
            return playerPos + rot * localOffset;
        }
 
        /// <summary>
        /// Направление выстрела: от вычисленного ствола в точку прицела.
        /// После компенсации yaw это направление почти совпадает
        /// с forward корпуса — но считаем честно, а не берём forward.
        /// </summary>
        public static bool TryGetFireDirection(
            Vector3 playerPos, float bodyYaw, Vector3 localOffset,
            Vector3 aimPoint, out Vector3 dir, out Vector3 muzzlePos)
        {
            muzzlePos = GetMuzzlePosition(playerPos, bodyYaw, localOffset);
 
            Vector3 raw = aimPoint - muzzlePos;
            raw.y = 0f;
 
            if (raw.sqrMagnitude < 0.0001f)
            {
                dir = Vector3.zero;
                return false;
            }
 
            dir = raw.normalized;
            return true;
        }
    }
}