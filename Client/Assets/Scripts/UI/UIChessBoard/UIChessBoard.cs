using UnityEngine;

public class UIChessBoard : MonoBehaviour
{
    [SerializeField]
    private ChessElement OriginPrefab;

    [SerializeField]
    private Transform ParentTransform;

    private ArrayList<ChessElement> chessElements;

    void Start()
    {
        InitChessBoard();
    }

    void InitChessBoard()
    {
        chessElements = new ArrayList<ChessElement>(GlobleValue.ChessWidth, GlobleValue.ChessHeight);

        for (int y = 0; y < GlobleValue.ChessHeight; y++)
        {
            for (int x = 0; x < GlobleValue.ChessWidth; x++)
            {
                var chessElement = InstanceChessElement(OriginPrefab);
                if (chessElement == null)
                    continue;

                chessElement.transform.SetParent(ParentTransform, false);
                chessElement.gameObject.SetActive(true);
                chessElement.InIt(new ChessElementData(x, y));
                chessElements.Set(x, y, chessElement);
            }
        }
    }

    public ChessElement GetChessElement(int x, int y)
    {
        return chessElements?.Get(x, y);
    }

    T InstanceChessElement<T>(T inOrigin) where T : UnityEngine.Object
    {
        var insPrefab = Instantiate<T>(inOrigin);
        if (insPrefab == null)
            return null;

        return insPrefab;
    }
}
