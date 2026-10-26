using System.Collections.Generic;
using Murim.Core.Martial;

namespace Murim.Core.Battle
{
    public enum BattleSide { Defender, Attacker }

    /// <summary>플레이어 명령: 그 칸으로 가거나, 그 적을 친다.</summary>
    public class BattleOrder
    {
        public bool Attack;
        public int TargetId = -1;
        public int X;
        public int Y;
    }

    /// <summary>전투 단위 하나: 이름 있는 인물이거나 이름 없는 문도·졸개.</summary>
    public class BattleUnit
    {
        public int Id;
        /// <summary>이름 없는 문도·졸개는 null.</summary>
        public string CharacterId;
        public string Name;
        public BattleSide Side;
        public bool IsLeader;
        public float X;
        public float Y;
        public int Hp;
        public int MaxHp;
        public float Force;
        public int MaxForce;
        public int Realm;
        public List<(CardDef card, ArtDef art, int mastery)> Deck = new List<(CardDef, ArtDef, int)>();
        public int MovementMastery;
        /// <summary>칸/초.</summary>
        public float Speed;
        public float AttackInterval;
        public float Cooldown;
        /// <summary>표식(부상 등) 공격 보정 배율.</summary>
        public double Modifier = 1;
        public bool Down;
        public bool Fled;
        public bool Fleeing;
        public BattleOrder Order;
        /// <summary>자동 교전 중인 적.</summary>
        public int TargetId = -1;
        public List<(int x, int y)> Path = new List<(int, int)>();
        public int PathGoalX = -1;
        public int PathGoalY = -1;
        public float RepathTimer;
        public int Downed;
        /// <summary>이동 명령으로 자리를 잡았다: 그 자리에서 닿는 적만 친다.</summary>
        public bool Holding;

        public bool Active => !Down && !Fled;
        public int CellX => (int)System.Math.Floor(X);
        public int CellY => (int)System.Math.Floor(Y);
        public bool Named => CharacterId != null;
    }
}
