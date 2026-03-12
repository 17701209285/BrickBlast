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

        for (int i = 0; i < GlobleValue.ChessHeight; i++)
        {
            for (int j = 0; j < GlobleValue.ChessWidth; j++)
            {
                var chessElement = InstanceChessElement(OriginPrefab);
                if (chessElement == null)
                    continue;

                chessElement.transform.SetParent(ParentTransform, false);
                chessElement.gameObject.SetActive(true);
                chessElement.InIt(new ChessElementData(j, i));
                chessElements.Set(j, i, chessElement);
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
