using ExileCore.Shared;
using SharpDX;
using System;
using System.Collections;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RectangleF = SharpDX.RectangleF;

namespace StrongboxRolling
{
    public class Mouse
    {
        public const int MOUSEEVENTF_MOVE = 0x0001;
        public const int MouseeventfLeftdown = 0x02;
        public const int MouseeventfLeftup = 0x04;
        public const int MouseeventfMiddown = 0x0020;
        public const int MouseeventfMidup = 0x0040;
        public const int MouseeventfRightdown = 0x0008;
        public const int MouseeventfRightup = 0x0010;
        public const int MouseEventWheel = 0x800;

        // 
        private const int MovementDelay = 10;
        private const int ClickDelay = 1;
        
        // Constants for keyboard events
        private const int VK_SHIFT = 0x10;
        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern bool BlockInput(bool fBlockIt);
        
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        /// <summary>
        /// Sets the cursor position relative to the game window.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="gameWindow"></param>
        /// <returns></returns>
        public static bool SetCursorPos(int x, int y, RectangleF gameWindow)
        {
            return SetCursorPos(x + (int)gameWindow.X, y + (int)gameWindow.Y);
        }

        /// <summary>
        /// Sets the cursor position to the center of a given rectangle relative to the game window
        /// </summary>
        /// <param name="position"></param>
        /// <param name="gameWindow"></param>
        /// <returns></returns>
        public static bool SetCurosPosToCenterOfRec(RectangleF position, RectangleF gameWindow)
        {
            return SetCursorPos((int)(gameWindow.X + position.Center.X), (int)(gameWindow.Y + position.Center.Y));
        }

        /// <summary>
        /// Retrieves the cursor's position, in screen coordinates.
        /// </summary>
        /// <see>See MSDN documentation for further information.</see>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point lpPoint);

        public static SharpDX.Point GetCursorPosition()
        {
            GetCursorPos(out var lpPoint);
            return lpPoint;
        }

        public static void LeftMouseDown()
        {
            mouse_event(MouseeventfLeftdown, 0, 0, 0, 0);
        }

        public static void LeftMouseUp()
        {
            mouse_event(MouseeventfLeftup, 0, 0, 0, 0);
        }

        public static void RightMouseDown()
        {
            mouse_event(MouseeventfRightdown, 0, 0, 0, 0);
        }

        public static void RightMouseUp()
        {
            mouse_event(MouseeventfRightup, 0, 0, 0, 0);
        }

        public static void SetCursorPosAndLeftClick(Vector2 coords, int extraDelay)
        {
            var posX = (int)coords.X;
            var posY = (int)coords.Y;
            SetCursorPos(posX, posY);
            Thread.Sleep(MovementDelay + extraDelay);
            mouse_event(MouseeventfLeftdown, 0, 0, 0, 0);
            Thread.Sleep(ClickDelay);
            mouse_event(MouseeventfLeftup, 0, 0, 0, 0);
        }

        public static void SetCursorPosAndLeftOrRightClick(Vector2 coords, int extraDelay, bool leftClick = true)
        {
            var posX = (int)coords.X;
            var posY = (int)coords.Y;
            SetCursorPos(posX, posY);
            Thread.Sleep(MovementDelay + extraDelay);

            if (leftClick)
                LeftClick(ClickDelay);
            else
                RightClick(ClickDelay);
        }

        public static void LeftClick(int extraDelay)
        {
            LeftMouseDown();
            if (extraDelay > 0) Thread.Sleep(ClickDelay);
            LeftMouseUp();
        }

        // New method to simulate left-clicking while holding Shift key
        public static void LeftClickWithShift(int extraDelay)
        {
            try
            {
                // Press Shift key down
                keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYDOWN, 0);
                
                // Small delay to ensure Shift is registered
                Thread.Sleep(10);
                
                // Perform left click
                LeftMouseDown();
                if (extraDelay > 0) Thread.Sleep(ClickDelay);
                LeftMouseUp();
                
                // Small delay before releasing Shift
                Thread.Sleep(10);
            }
            finally
            {
                // Always release Shift key
                keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
            }
        }

        // Method to start holding Shift key
        public static void StartHoldingShift()
        {
            // Press Shift key down
            keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYDOWN, 0);
            
            // Small delay to ensure Shift is registered
            Thread.Sleep(10);
        }
        
        // Method to stop holding Shift key
        public static void StopHoldingShift()
        {
            // Release Shift key
            keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
            
            // Small delay to ensure Shift is released
            Thread.Sleep(10);
        }
        
        // Method to perform left click while Shift is already being held
        public static void LeftClickWhileShiftHeld(int extraDelay)
        {
            // Perform left click without touching Shift key
            LeftMouseDown();
            if (extraDelay > 0) Thread.Sleep(ClickDelay);
            LeftMouseUp();
        }

        public static void RightClick(int extraDelay)
        {
            RightMouseDown();
            Thread.Sleep(ClickDelay);
            RightMouseUp();
        }

        public static void VerticalScroll(bool forward, int clicks)
        {
            if (forward)
                mouse_event(MouseEventWheel, 0, 0, clicks * 120, 0);
            else
                mouse_event(MouseEventWheel, 0, 0, -(clicks * 120), 0);
        }
        ////////////////////////////////////////////////////////////

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;

            public static implicit operator SharpDX.Point(Point point)
            {
                return new SharpDX.Point(point.X, point.Y);
            }
        }

        #region MyFix

        private static void SetCursorPosition(float x, float y)
        {
            SetCursorPos((int)x, (int)y);
        }

        public static Vector2 GetCursorPositionVector()
        {
            var currentMousePoint = GetCursorPosition();
            return new Vector2(currentMousePoint.X, currentMousePoint.Y);
        }

        public static void SetCursorPosition(Vector2 end)
        {
            var cursor = GetCursorPositionVector();
            var stepVector2 = new Vector2();
            var step = (float)Math.Sqrt(Vector2.Distance(cursor, end)) * 1.618f;
            if (step > 275) step = 240;
            stepVector2.X = (end.X - cursor.X) / step;
            stepVector2.Y = (end.Y - cursor.Y) / step;
            var fX = cursor.X;
            var fY = cursor.Y;

            for (var j = 0; j < step; j++)
            {
                fX += +stepVector2.X;
                fY += stepVector2.Y;
                SetCursorPosition(fX, fY);
                Thread.Sleep(2);
            }
        }

        public static void SetCursorPosAndLeftClickHuman(Vector2 coords, int extraDelay)
        {
            SetCursorPosition(coords);
            Thread.Sleep(MovementDelay + extraDelay);
            LeftMouseDown();
            Thread.Sleep(MovementDelay + extraDelay);
            LeftMouseUp();
        }

        public static void SetCursorPos(Vector2 vec)
        {
            SetCursorPos((int)vec.X, (int)vec.Y);
        }

        public static void MoveCursorToPosition(Vector2 vec)
        {
            SetCursorPos((int)vec.X, (int)vec.Y);
            MouseMove();
        }

        public static float speedMouse;

        public static void SetCursorPosHuman(Vector2 vec, float speed = 1f)
        {
            var step = (float)Math.Sqrt(Vector2.Distance(GetCursorPositionVector(), vec)) * speed / 20;

            if (step > 6)
            {
                for (var i = 0; i < step; i++)
                {
                    var vector2 = Vector2.SmoothStep(GetCursorPositionVector(), vec, i / step);
                    SetCursorPos((int)vector2.X, (int)vector2.Y);
                    Task.Delay(30).Wait();
                }
            }
            else
                SetCursorPos(vec);
        }
        public static void LinearSmoothMove(Vector2 newPosition, int steps = 80, int msDelay = 1)
        {
            //System.Drawing.Point start = new();
            Vector2 startv = GetCursorPositionVector();
            Point start = new();
            start.X = (int)startv.X;
            start.Y = (int)startv.Y;
            PointF iterPoint = new(start.X, start.Y);

            // Find the slope of the line segment defined by start and newPosition
            PointF slope = new PointF(newPosition.X - start.X, newPosition.Y - start.Y);

            // Divide by the number of steps
            slope.X = slope.X / steps;
            slope.Y = slope.Y / steps;


            // Move the mouse to each iterative point.
            for (int i = 0; i < steps; i++)
            {
                iterPoint = new PointF(iterPoint.X + slope.X, iterPoint.Y + slope.Y);
                Vector2 newV = new(System.Drawing.Point.Round(iterPoint).X, System.Drawing.Point.Round(iterPoint).Y);
                SetCursorPos(newV);
                if (i > steps * .8)
                {
                    Thread.Sleep(msDelay);
                }
            }

            // Move the mouse to the final destination.
            SetCursorPos(new Vector2(newPosition.X, newPosition.Y));
        }

        public static IEnumerator LeftClick()
        {
            LeftMouseDown();
            yield return new WaitTime(2);
            LeftMouseUp();
        }

        public static void MouseMove()
        {
            mouse_event(MOUSEEVENTF_MOVE, 0, 0, 0, 0);
        }

        #endregion
    }
}
