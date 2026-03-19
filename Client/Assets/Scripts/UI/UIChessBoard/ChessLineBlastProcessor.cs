internal static class ChessLineBlastProcessor
{
    public static void TriggerHorizontal(UIChessBoard board, ChessElement origin)
    {
        if (board == null || origin == null)
        {
            return;
        }

        var rowIndex = origin.Y;
        for (int x = 0; x < board.BoardWidth; x++)
        {
            if (x == origin.X)
            {
                continue;
            }

            board.ApplyBlastDamageToTarget(board.GetChessElement(x, rowIndex), ChessDamageSource.HorizontalBlast);
        }
    }

    public static void TriggerVertical(UIChessBoard board, ChessElement origin)
    {
        if (board == null || origin == null)
        {
            return;
        }

        var columnIndex = origin.X;
        for (int y = 0; y < board.BoardHeight; y++)
        {
            if (y == origin.Y)
            {
                continue;
            }

            board.ApplyBlastDamageToTarget(board.GetChessElement(columnIndex, y), ChessDamageSource.VerticalBlast);
        }
    }
}
