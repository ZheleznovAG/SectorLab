using System;
using SectorLab.Units;

namespace SectorLab.Battle
{
    public enum UnitLife : byte { Loading, Alive, Respawning, Spawning }

    // A roster slot as seen by a player. Contains no scene object or local respawn process.
    public readonly struct UnitView : IEquatable<UnitView>
    {
        public readonly int Id, Health, MaxHealth;
        public readonly Team Team;
        public readonly UnitRole Role;
        public readonly UnitLife Life;
        public readonly float RespawnSeconds, X, Z, ShotX, ShotZ;
        public readonly uint Generation, Shots;
        public bool Alive => Life == UnitLife.Alive;

        public UnitView(int id, Team team, UnitRole role, UnitLife life, int health, int maxHealth,
            float respawnSeconds, uint generation, uint shots, float x, float z, float shotX = 0, float shotZ = 0)
        {
            Id = id; Team = team; Role = role; Life = life; Health = health; MaxHealth = maxHealth;
            RespawnSeconds = respawnSeconds; Generation = generation; Shots = shots; X = x; Z = z;
            ShotX = shotX; ShotZ = shotZ;
        }

        public bool Equals(UnitView other) => Id == other.Id && Team == other.Team && Role == other.Role
            && Life == other.Life && Health == other.Health && MaxHealth == other.MaxHealth
            && RespawnSeconds == other.RespawnSeconds && Generation == other.Generation && Shots == other.Shots
            && X == other.X && Z == other.Z && ShotX == other.ShotX && ShotZ == other.ShotZ;
        public override bool Equals(object obj) => obj is UnitView other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Team, Role, Life, Health, Generation, X, Z);
    }
}
