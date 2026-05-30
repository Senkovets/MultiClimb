using System.Linq;
using _Project.CodeBase;
using Fusion;
using UnityEngine;

namespace MultiClimb.Match
{
    public sealed class LeaderboardSystem : NetworkBehaviour
    {
        [SerializeField] private PlayerRegistry registry;

        [Tooltip("��� ����� ������������� ��������� (� ����� Fusion). 0 = ������ ���.")]
        [SerializeField] private int updateEveryNTicks = 10;

        private int _lastTick = -999999;

        public void Tick()
        {
            if (registry == null) return;
            if (Runner == null) return;
            if (Runner.IsResimulation) return;

            if (updateEveryNTicks > 0 && (Runner.Tick - _lastTick) < updateEveryNTicks)
                return;

            _lastTick = Runner.Tick;

            // ��������� �� Score (��� � ���� ����)
            var sorted = registry.Players
                .OrderByDescending(p => p.Value.Score)
                .ToArray();

            MatchEventBus.Instance?.Raise(new LeaderboardChangedEvent(sorted));
        }
    }
}
