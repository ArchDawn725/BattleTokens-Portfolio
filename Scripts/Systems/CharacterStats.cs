public class CharacterStats
{
    #region STATS

    public string Name { get; set; }

    public int MaxHealth { get; private set; }
    public int Health { get; private set; }

    public int Defence { get; private set; }
    public int DamageBonus { get; private set; }

    public int StartingActionPoints { get; private set; }
    public int ActionPoints { get; private set; }

    public ClassSpecial ClassSpecial { get; private set; }
    public int CritChance { get; private set; }
    public float CritMultiplier { get; private set; }
    public int[] ActionIds { get; private set; }

    // ---------- Temporary stats (per turn / effects) ----------
    public int TempDef { get; private set; }
    public int TempDmg { get; private set; }
    public int TempRegen { get; private set; } // >0 regen, <0 poison

    public bool Zombified { get; private set; }
    public bool UndeadResistance { get; private set; }
    public bool Webbed { get; private set; }
    public string ProtectedBy { get; private set; }

    public bool IsDead => Health <= 0;
    public int GetTotalDefence() => Defence + TempDef;
    public int GetTotalDamageBonus() => DamageBonus + TempDmg;

    #endregion

    #region STAT_MUTATORS

    public void AdjustHealth(int val) => Health += val;
    public void AdjustTempDef(int val) => TempDef += val;
    public void AdjustTempDmg(int val) => TempDmg += val;
    public void AdjustTempRegen(int val) => TempRegen += val;

    public void SetZombified(bool val) => Zombified = val;
    public void SetWebbed(bool val) => Webbed = val;
    public void SetProtectedBy(string val) => ProtectedBy = val;
    public void SetUndeadResistance(bool val) => UndeadResistance = val;

    public void AdjustMaxHealth(int val) => MaxHealth += val;
    public void AdjustDefence(int val) => Defence += val;
    public void AdjustDamageBonus(int val) => DamageBonus += val;
    public void AdjustActionPoints(int val) => ActionPoints += val;

    public void CapHealth()
    {
        if (Health > MaxHealth)
            Health = MaxHealth;
    }

    public void ActionPointsNewTurn() => ActionPoints += StartingActionPoints;

    public void SetHealth(int val) => Health = val;
    public void SetHealthRegen(int val) => TempRegen = val;
    public void SetTempDef(int val) => TempDef = val;

    #endregion

    #region CONSTRUCTION

    public CharacterStats(CharacterVariables variables)
    {
        Name = variables.Name;

        Health = variables.Health;
        MaxHealth = variables.Health;

        Defence = variables.Defence;
        DamageBonus = variables.Damage;

        ClassSpecial = variables.ClassSpecial;

        StartingActionPoints = variables.ActionPoints;
        ActionPoints = StartingActionPoints; // if you want to start full

        CritChance = variables.CritChance;
        CritMultiplier = variables.CritMultiplier;

        ActionIds = variables.ActionIds;
    }

    #endregion
}
