using UnityEngine;

public class ChessElement : MonoBehaviour
{
    private ChessElementData ChessData;

    public int X => ChessData.X;
    public int Y => ChessData.Y;

    public void InIt(ChessElementData InData)
    {
        ChessData = InData;
    }
}
