using UnityEngine;

public readonly struct ChessHitEffectContext
{
    public ChessElement Target { get; }
    public Vector2 HitPointInBoardSpace { get; }
    public int Damage { get; }
    public int PreviousLife { get; }
    public int CurrentLife { get; }
    public bool IsDestroyed => CurrentLife <= 0;

    public ChessHitEffectContext(ChessElement target, Vector2 hitPointInBoardSpace, int damage, int previousLife, int currentLife)
    {
        Target = target;
        HitPointInBoardSpace = hitPointInBoardSpace;
        Damage = damage;
        PreviousLife = previousLife;
        CurrentLife = currentLife;
    }
}

public interface IChessHitEffectPlayer
{
    void PlayHitEffect(in ChessHitEffectContext context);
}
