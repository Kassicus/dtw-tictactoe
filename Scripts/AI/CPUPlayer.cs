using Godot;
using System.Collections.Generic;

public class CPUPlayer
{
    private const int MaxDepth = 2;
    private const int MaxMovesToConsider = 15;
    private const int WinGameScore = 10000;
    private const int WinBoardScore = 500;
    private const int TwoInRowScore = 50;
    private const int CenterBoardBonus = 100;
    private const int CornerBoardBonus = 50;
    private const int CenterCellBonus = 20;
    private const int CornerCellBonus = 10;

    private static readonly int[][] WinPatterns = new int[][]
    {
        new[] { 0, 0, 1, 0, 2, 0 },
        new[] { 0, 1, 1, 1, 2, 1 },
        new[] { 0, 2, 1, 2, 2, 2 },
        new[] { 0, 0, 0, 1, 0, 2 },
        new[] { 1, 0, 1, 1, 1, 2 },
        new[] { 2, 0, 2, 1, 2, 2 },
        new[] { 0, 0, 1, 1, 2, 2 },
        new[] { 2, 0, 1, 1, 0, 2 }
    };

    private struct BoardState
    {
        public Player[] Cells;        // Flat array: 81 cells
        public Player[] BoardWinners; // 9 small boards
        public Player GameWinner;
        public Player CurrentPlayer;

        public static BoardState Create()
        {
            return new BoardState
            {
                Cells = new Player[81],
                BoardWinners = new Player[9],
                GameWinner = Player.None,
                CurrentPlayer = Player.X
            };
        }

        public BoardState Clone()
        {
            return new BoardState
            {
                Cells = (Player[])Cells.Clone(),
                BoardWinners = (Player[])BoardWinners.Clone(),
                GameWinner = GameWinner,
                CurrentPlayer = CurrentPlayer
            };
        }

        public Player GetCell(int bx, int by, int cx, int cy)
        {
            return Cells[(bx * 3 + by) * 9 + (cx * 3 + cy)];
        }

        public void SetCell(int bx, int by, int cx, int cy, Player player)
        {
            Cells[(bx * 3 + by) * 9 + (cx * 3 + cy)] = player;
        }

        public Player GetBoardWinner(int bx, int by)
        {
            return BoardWinners[bx * 3 + by];
        }

        public void SetBoardWinner(int bx, int by, Player player)
        {
            BoardWinners[bx * 3 + by] = player;
        }
    }

    private struct Move
    {
        public int BoardX, BoardY, CellX, CellY;
        public int Priority;

        public Move(int bx, int by, int cx, int cy, int priority = 0)
        {
            BoardX = bx; BoardY = by; CellX = cx; CellY = cy; Priority = priority;
        }
    }

    public (SmallBoard board, Cell cell) GetBestMove(BoardController boardController, Player cpuPlayer)
    {
        var state = CreateStateFromBoard(boardController);
        state.CurrentPlayer = cpuPlayer;
        Player opponent = cpuPlayer == Player.X ? Player.O : Player.X;

        var moves = GetValidMoves(state);
        if (moves.Count == 0) return FindAnyValidMove(boardController);

        // Check for immediate winning move
        foreach (var move in moves)
        {
            var testState = ApplyMove(state, move);
            if (testState.GameWinner == cpuPlayer)
            {
                return GetMoveResult(boardController, move);
            }
        }

        // Check for blocking opponent's winning move
        state.CurrentPlayer = opponent;
        var oppMoves = GetValidMoves(state);
        foreach (var move in oppMoves)
        {
            var testState = ApplyMove(state, move);
            if (testState.GameWinner == opponent)
            {
                // Block this move if it's in our valid moves
                foreach (var m in moves)
                {
                    if (m.BoardX == move.BoardX && m.BoardY == move.BoardY &&
                        m.CellX == move.CellX && m.CellY == move.CellY)
                    {
                        return GetMoveResult(boardController, m);
                    }
                }
            }
        }
        state.CurrentPlayer = cpuPlayer;

        // Check for blocking opponent's small board wins
        state.CurrentPlayer = opponent;
        foreach (var move in oppMoves)
        {
            var testState = ApplyMove(state, move);
            if (testState.GetBoardWinner(move.BoardX, move.BoardY) == opponent &&
                state.GetBoardWinner(move.BoardX, move.BoardY) == Player.None)
            {
                // Block this move if we can
                foreach (var m in moves)
                {
                    if (m.BoardX == move.BoardX && m.BoardY == move.BoardY &&
                        m.CellX == move.CellX && m.CellY == move.CellY)
                    {
                        return GetMoveResult(boardController, m);
                    }
                }
            }
        }
        state.CurrentPlayer = cpuPlayer;

        // Check for small board winning moves (take our own wins)
        foreach (var move in moves)
        {
            var testState = ApplyMove(state, move);
            if (testState.GetBoardWinner(move.BoardX, move.BoardY) == cpuPlayer &&
                state.GetBoardWinner(move.BoardX, move.BoardY) == Player.None)
            {
                return GetMoveResult(boardController, move);
            }
        }

        // Use minimax for remaining moves, but limit to top candidates
        ScoreAndSortMoves(moves, state, cpuPlayer);
        if (moves.Count > MaxMovesToConsider)
        {
            moves = moves.GetRange(0, MaxMovesToConsider);
        }

        Move? bestMove = null;
        int bestScore = int.MinValue;
        int alpha = int.MinValue;
        int beta = int.MaxValue;

        foreach (var move in moves)
        {
            var newState = ApplyMove(state, move);
            int score = Minimax(newState, MaxDepth - 1, alpha, beta, false, cpuPlayer);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
            alpha = System.Math.Max(alpha, score);
        }

        if (bestMove.HasValue)
        {
            return GetMoveResult(boardController, bestMove.Value);
        }

        return FindAnyValidMove(boardController);
    }

    private (SmallBoard, Cell) GetMoveResult(BoardController bc, Move m)
    {
        var board = bc.SmallBoards[m.BoardX, m.BoardY];
        var cell = board.Cells[m.CellX, m.CellY];
        return (board, cell);
    }

    private BoardState CreateStateFromBoard(BoardController boardController)
    {
        var state = BoardState.Create();

        for (int bx = 0; bx < 3; bx++)
        {
            for (int by = 0; by < 3; by++)
            {
                var smallBoard = boardController.SmallBoards[bx, by];
                state.SetBoardWinner(bx, by, smallBoard.Winner);

                for (int cx = 0; cx < 3; cx++)
                {
                    for (int cy = 0; cy < 3; cy++)
                    {
                        state.SetCell(bx, by, cx, cy, smallBoard.Cells[cx, cy].OccupiedBy);
                    }
                }
            }
        }

        return state;
    }

    private int Minimax(BoardState state, int depth, int alpha, int beta, bool maximizing, Player cpuPlayer)
    {
        if (state.GameWinner != Player.None)
        {
            return state.GameWinner == cpuPlayer ? WinGameScore + depth : -WinGameScore - depth;
        }

        if (depth <= 0)
        {
            return Evaluate(state, cpuPlayer);
        }

        var moves = GetValidMoves(state);
        if (moves.Count == 0) return 0;

        // Limit moves at deeper levels
        if (moves.Count > MaxMovesToConsider)
        {
            ScoreAndSortMoves(moves, state, cpuPlayer);
            moves = moves.GetRange(0, MaxMovesToConsider);
        }

        if (maximizing)
        {
            int maxEval = int.MinValue;
            foreach (var move in moves)
            {
                var newState = ApplyMove(state, move);
                int eval = Minimax(newState, depth - 1, alpha, beta, false, cpuPlayer);
                maxEval = System.Math.Max(maxEval, eval);
                alpha = System.Math.Max(alpha, eval);
                if (beta <= alpha) break;
            }
            return maxEval;
        }
        else
        {
            int minEval = int.MaxValue;
            foreach (var move in moves)
            {
                var newState = ApplyMove(state, move);
                int eval = Minimax(newState, depth - 1, alpha, beta, true, cpuPlayer);
                minEval = System.Math.Min(minEval, eval);
                beta = System.Math.Min(beta, eval);
                if (beta <= alpha) break;
            }
            return minEval;
        }
    }

    private void ScoreAndSortMoves(List<Move> moves, BoardState state, Player cpuPlayer)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            var m = moves[i];
            m.Priority = GetMovePriority(m, state, cpuPlayer);
            moves[i] = m;
        }
        moves.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    private int GetMovePriority(Move move, BoardState state, Player cpuPlayer)
    {
        int priority = 0;
        Player opponent = cpuPlayer == Player.X ? Player.O : Player.X;

        // Strongly prefer center board
        if (move.BoardX == 1 && move.BoardY == 1) priority += 100;
        // Prefer corners
        else if ((move.BoardX == 0 || move.BoardX == 2) && (move.BoardY == 0 || move.BoardY == 2))
            priority += 60;

        // Prefer center cell
        if (move.CellX == 1 && move.CellY == 1) priority += 50;
        // Prefer corner cells
        else if ((move.CellX == 0 || move.CellX == 2) && (move.CellY == 0 || move.CellY == 2))
            priority += 30;

        // Check if move wins a small board for us
        var testState = ApplyMove(state, move);
        if (testState.GetBoardWinner(move.BoardX, move.BoardY) == cpuPlayer &&
            state.GetBoardWinner(move.BoardX, move.BoardY) == Player.None)
        {
            priority += 500;
        }

        // Check if move blocks opponent from winning a small board
        int opponentThreats = CountWinningThreats(state, move.BoardX, move.BoardY, opponent);
        if (opponentThreats > 0 && IsBlockingMove(state, move, opponent))
        {
            priority += 400; // High priority to block
        }

        // Check if move creates two-in-a-row in small board
        priority += CountThreats(state, move, cpuPlayer) * 20;

        // Penalize moves in boards opponent is close to winning
        priority -= opponentThreats * 30;

        return priority;
    }

    private int CountWinningThreats(BoardState state, int bx, int by, Player player)
    {
        int threats = 0;
        foreach (var pattern in WinPatterns)
        {
            int count = 0, empty = 0;
            for (int i = 0; i < 3; i++)
            {
                var cell = state.GetCell(bx, by, pattern[i * 2], pattern[i * 2 + 1]);
                if (cell == player) count++;
                else if (cell == Player.None) empty++;
            }
            if (count == 2 && empty == 1) threats++;
        }
        return threats;
    }

    private bool IsBlockingMove(BoardState state, Move move, Player opponent)
    {
        int bx = move.BoardX, by = move.BoardY;
        foreach (var pattern in WinPatterns)
        {
            int count = 0;
            bool moveInPattern = false;

            for (int i = 0; i < 3; i++)
            {
                int cx = pattern[i * 2];
                int cy = pattern[i * 2 + 1];
                if (cx == move.CellX && cy == move.CellY) moveInPattern = true;
                if (state.GetCell(bx, by, cx, cy) == opponent) count++;
            }

            // If opponent has 2 in this line and our move is in this line, we're blocking
            if (count == 2 && moveInPattern) return true;
        }
        return false;
    }

    private int CountThreats(BoardState state, Move move, Player player)
    {
        int threats = 0;
        int bx = move.BoardX, by = move.BoardY;

        foreach (var pattern in WinPatterns)
        {
            bool inPattern = false;
            int playerCount = 0;
            int emptyCount = 0;

            for (int i = 0; i < 3; i++)
            {
                int cx = pattern[i * 2];
                int cy = pattern[i * 2 + 1];
                if (cx == move.CellX && cy == move.CellY) inPattern = true;

                var cell = state.GetCell(bx, by, cx, cy);
                if (cell == player) playerCount++;
                else if (cell == Player.None) emptyCount++;
            }

            if (inPattern && playerCount == 1 && emptyCount == 2)
            {
                threats++;
            }
        }

        return threats;
    }

    private int Evaluate(BoardState state, Player cpuPlayer)
    {
        Player opponent = cpuPlayer == Player.X ? Player.O : Player.X;
        int score = 0;

        // Evaluate main board control
        for (int bx = 0; bx < 3; bx++)
        {
            for (int by = 0; by < 3; by++)
            {
                var winner = state.GetBoardWinner(bx, by);
                int posBonus = (bx == 1 && by == 1) ? CenterBoardBonus :
                    ((bx == 0 || bx == 2) && (by == 0 || by == 2)) ? CornerBoardBonus : 30;

                if (winner == cpuPlayer) score += WinBoardScore + posBonus;
                else if (winner == opponent) score -= WinBoardScore + posBonus;
                else
                {
                    // Evaluate the small board position
                    score += EvaluateSmallBoard(state, bx, by, cpuPlayer);
                    score -= EvaluateSmallBoard(state, bx, by, opponent);
                }
            }
        }

        // Evaluate main board threats
        score += EvaluateMainBoardThreats(state, cpuPlayer) * 200;
        score -= EvaluateMainBoardThreats(state, opponent) * 200;

        return score;
    }

    private int EvaluateSmallBoard(BoardState state, int bx, int by, Player player)
    {
        int score = 0;

        // Center cell
        if (state.GetCell(bx, by, 1, 1) == player) score += CenterCellBonus;

        // Corner cells
        if (state.GetCell(bx, by, 0, 0) == player) score += CornerCellBonus;
        if (state.GetCell(bx, by, 0, 2) == player) score += CornerCellBonus;
        if (state.GetCell(bx, by, 2, 0) == player) score += CornerCellBonus;
        if (state.GetCell(bx, by, 2, 2) == player) score += CornerCellBonus;

        // Two-in-a-row patterns
        foreach (var pattern in WinPatterns)
        {
            int count = 0, empty = 0;
            for (int i = 0; i < 3; i++)
            {
                var cell = state.GetCell(bx, by, pattern[i * 2], pattern[i * 2 + 1]);
                if (cell == player) count++;
                else if (cell == Player.None) empty++;
            }
            if (count == 2 && empty == 1) score += TwoInRowScore;
        }

        return score;
    }

    private int EvaluateMainBoardThreats(BoardState state, Player player)
    {
        int threats = 0;

        foreach (var pattern in WinPatterns)
        {
            int count = 0, empty = 0;
            for (int i = 0; i < 3; i++)
            {
                var winner = state.BoardWinners[pattern[i * 2] * 3 + pattern[i * 2 + 1]];
                if (winner == player) count++;
                else if (winner == Player.None) empty++;
            }
            if (count == 2 && empty == 1) threats++;
        }

        return threats;
    }

    private List<Move> GetValidMoves(BoardState state)
    {
        var moves = new List<Move>();

        for (int bx = 0; bx < 3; bx++)
        {
            for (int by = 0; by < 3; by++)
            {
                if (state.GetBoardWinner(bx, by) != Player.None) continue;

                for (int cx = 0; cx < 3; cx++)
                {
                    for (int cy = 0; cy < 3; cy++)
                    {
                        if (state.GetCell(bx, by, cx, cy) == Player.None)
                        {
                            moves.Add(new Move(bx, by, cx, cy));
                        }
                    }
                }
            }
        }

        return moves;
    }

    private BoardState ApplyMove(BoardState state, Move move)
    {
        var newState = state.Clone();
        newState.SetCell(move.BoardX, move.BoardY, move.CellX, move.CellY, state.CurrentPlayer);

        // Check small board win
        if (CheckSmallBoardWin(newState, move.BoardX, move.BoardY, state.CurrentPlayer))
        {
            newState.SetBoardWinner(move.BoardX, move.BoardY, state.CurrentPlayer);

            if (CheckGameWin(newState, state.CurrentPlayer))
            {
                newState.GameWinner = state.CurrentPlayer;
            }
        }

        newState.CurrentPlayer = state.CurrentPlayer == Player.X ? Player.O : Player.X;
        return newState;
    }

    private bool CheckSmallBoardWin(BoardState state, int bx, int by, Player player)
    {
        foreach (var pattern in WinPatterns)
        {
            if (state.GetCell(bx, by, pattern[0], pattern[1]) == player &&
                state.GetCell(bx, by, pattern[2], pattern[3]) == player &&
                state.GetCell(bx, by, pattern[4], pattern[5]) == player)
            {
                return true;
            }
        }
        return false;
    }

    private bool CheckGameWin(BoardState state, Player player)
    {
        foreach (var pattern in WinPatterns)
        {
            if (state.BoardWinners[pattern[0] * 3 + pattern[1]] == player &&
                state.BoardWinners[pattern[2] * 3 + pattern[3]] == player &&
                state.BoardWinners[pattern[4] * 3 + pattern[5]] == player)
            {
                return true;
            }
        }
        return false;
    }

    private (SmallBoard board, Cell cell) FindAnyValidMove(BoardController boardController)
    {
        for (int bx = 0; bx < 3; bx++)
        {
            for (int by = 0; by < 3; by++)
            {
                var smallBoard = boardController.SmallBoards[bx, by];
                if (smallBoard.IsWon) continue;

                for (int cx = 0; cx < 3; cx++)
                {
                    for (int cy = 0; cy < 3; cy++)
                    {
                        var cell = smallBoard.Cells[cx, cy];
                        if (!cell.IsOccupied)
                        {
                            return (smallBoard, cell);
                        }
                    }
                }
            }
        }
        return (null, null);
    }
}
