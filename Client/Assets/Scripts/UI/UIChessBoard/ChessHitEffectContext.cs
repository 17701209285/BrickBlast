using UnityEngine;

public readonly struct ChessHitEffectContext
{
    public ChessElement Target { get; }
    public Vector2 HitPointInBoardSpace { get; }
    public ChessDamageSource DamageSource { get; }
    public int Damage { get; }
    public int PreviousLife { get; }
    public int CurrentLife { get; }
    public Color PreHitColor { get; }
    public bool IsDestroyed => CurrentLife <= 0;

    public ChessHitEffectContext(
        ChessElement target,
        Vector2 hitPointInBoardSpace,
        ChessDamageSource damageSource,
        int damage,
        int previousLife,
        int currentLife,
        Color preHitColor)
    {
        Target = target;
        HitPointInBoardSpace = hitPointInBoardSpace;
        DamageSource = damageSource;
        Damage = damage;
        PreviousLife = previousLife;
        CurrentLife = currentLife;
        PreHitColor = preHitColor;
    }
}

public interface IChessHitEffectPlayer
{
    void PlayHitEffect(in ChessHitEffectContext context);
}
