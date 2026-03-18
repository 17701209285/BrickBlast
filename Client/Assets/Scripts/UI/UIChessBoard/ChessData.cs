
public class ChessElementData
{
    public int X { get; private set; }
    public int Y { get; private set; }

    public ChessElementData(int inX,int inY)
    {
        SetPosition(inX, inY);
    }

    public void SetPosition(int inX, int inY)
    {
        X = inX;
        Y = inY;
    }


    public override string ToString()
    {
        return $"X:{X} Y:{Y}";
    }
}
