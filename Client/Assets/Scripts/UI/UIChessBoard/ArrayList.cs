using UnityEngine;

public class ArrayList<T> where T : class 
{
    private T[] items;
    private int width;
    private int height;

    public T this[int x, int y]
    {
        get => Get(x, y);
        set => Set(x, y,value);
    }

    public ArrayList(int InWidth,int InHeight) 
    {
        this.width = InWidth;
        this.height = InHeight;
        items = new T[InWidth * InHeight];
    }

    public int ToIndex(int x, int y)
    {
        return y * width + x;
    }

    public bool IsValid(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public void Set(int x, int y, T value)
    {
        if(!IsValid(x,y))
        {
            return;
        }
        items[ToIndex(x, y)] = value;
    }

    public T Get(int x, int y)
    {
        if (!IsValid(x, y))
        {
            return null;
        }
        return items[ToIndex(x, y)];
    }
}
