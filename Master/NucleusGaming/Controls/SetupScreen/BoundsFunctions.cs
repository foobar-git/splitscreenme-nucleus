﻿using Nucleus.Gaming.Coop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Nucleus.Gaming.Controls.SetupScreen
{
    internal static class BoundsFunctions
    {
        public static void Initialize(SetupScreenControl _parent, UserGameInfo _userGameInfo, GameProfile _profile)
        {
            parent = _parent;
            userGameInfo = _userGameInfo;
            profile = _profile;
        }

        private static SetupScreenControl parent;
        private static UserGameInfo userGameInfo;
        private static GameProfile profile;
        internal static PlayerInfo selectedPlayer;

        public static UserScreen[] screens;

        internal static PointF draggingOffset;

        internal static int draggingIndex = -1;
        internal static int draggingScreen = -1;
        internal static int destBoundsScale;

        internal static Rectangle totalBounds;
        internal static RectangleF sizer;
        internal static RectangleF sizerBtnLeft;
        internal static RectangleF sizerBtnRight;
        internal static RectangleF sizerBtnTop;
        internal static RectangleF sizerBtnBottom;
        internal static RectangleF sizerBtnCenter;

        internal static RectangleF destEditBounds;
        internal static Rectangle destMonitorBounds;

        internal static bool dragging = false;


        internal static string CalculAspectRatio(PlayerInfo selectedPlayer)
        {
            int width = selectedPlayer.MonitorBounds.Width;
            int height = selectedPlayer.MonitorBounds.Height;

            int fwidth = selectedPlayer.MonitorBounds.Width;
            int fheight = selectedPlayer.MonitorBounds.Height;

            float ratio = (float)width / height;

            while (fwidth != 0 && fheight != 0)
            {
                if (fwidth > fheight)
                    fwidth %= fheight;
                else
                    fheight %= fwidth;
            }

            float val1 = (fwidth != 0 ? (width / fwidth) : (width / fheight));
            float val2 = (fwidth != 0 ? ((width / fwidth) / ratio) : ((width / fheight) / ratio));

            var spb = selectedPlayer.MonitorBounds;
            return $"[Resolution: {spb.Width} X {spb.Height}  Aspect Ratio: {val1} : {val2}  Top: {spb.Top}  Bottom: {spb.Bottom}  Left: {spb.Left}  Right: {spb.Right}]";
        }


        internal static RectangleF GetDefaultBounds(int index)
        {
            RectangleF playersArea = DevicesFunctions.playersArea;
            float playerSize = DevicesFunctions.playerSize;
            float lineWidth = index * playerSize;
            float line = (float)Math.Round(((lineWidth + playerSize) / (double)playersArea.Width) - 0.5);
            int perLine = (int)Math.Round((playersArea.Width / (double)playerSize) - 0.5);

            float x = playersArea.X + (index * playerSize) - (perLine * playerSize * line);
            float y = playersArea.Y + (playerSize * line);

            return new RectangleF(x, y, playerSize, playerSize);
        }


        internal static void UpdateScreens()
        {
            if (screens == null)
            {
                screens = ScreensUtil.AllScreens();
                totalBounds = RectangleUtil.Union(screens);
            }
            else
            {
                for (int i = 0; i < screens.Length; i++)
                {
                    screens[i].PlayerOnScreen = 0;
                }

                UserScreen[] newScreens = ScreensUtil.AllScreens();
                Rectangle newBounds = RectangleUtil.Union(newScreens);

                if (newBounds.Equals(totalBounds))
                {
                    return;
                }

                ///screens got updated, need to reflect in our window
                screens = newScreens;
                totalBounds = newBounds;

                ///remove all players screens
                List<PlayerInfo> playerData = profile.DevicesList;
                if (playerData != null)
                {
                    for (int i = 0; i < playerData.Count; i++)
                    {
                        PlayerInfo player = playerData[i];
                        player.EditBounds = GetDefaultBounds(draggingIndex);
                        player.ScreenIndex = -1;
                        player.DisplayIndex = -1;
                    }
                }
            }

            RectangleF screensArea;

            float screensAreaScale;

            if (screens.Length > 1)
            {
                screensArea = new RectangleF(5.0F, (float)parent.Height / 2.3f, (float)parent.Width - 20.0F, (float)parent.Height / 2.1f);
            }
            else
            {
                screensArea = new RectangleF(10.0F, 50.0F + (float)parent.Height * 0.2f + 10F, (float)parent.Width - 20.0F, (float)parent.Height * 0.5f);
            }

            screensAreaScale = screensArea.Width / (float)totalBounds.Width;

            if ((float)totalBounds.Height * screensAreaScale > screensArea.Height)
            {
                screensAreaScale = (float)screensArea.Height / (float)totalBounds.Height;
            }

            RectangleF scaledBounds = RectangleUtil.Scale(totalBounds, screensAreaScale);
            scaledBounds.X = screensArea.X;
            scaledBounds.Y = screensArea.Y;

            int minY = 0;
            for (int i = 0; i < screens.Length; i++)
            {
                UserScreen screen = screens[i];
                screen.priority = screen.MonitorBounds.X + screen.MonitorBounds.Y;
                Rectangle bounds = RectangleUtil.Scale(screen.MonitorBounds, screensAreaScale);

                RectangleF uiBounds = new RectangleF((float)bounds.X, (float)bounds.Y + scaledBounds.Y, (float)bounds.Width, (float)bounds.Height);

                screen.UIBounds = uiBounds;

                minY = Math.Min(minY, (int)uiBounds.X);
            }

            ///remove negative monitors
            minY = -minY;
            for (int i = 0; i < screens.Length; i++)
            {
                UserScreen screen = screens[i];

                RectangleF uiBounds = screen.UIBounds;

                uiBounds.X += (float)minY + scaledBounds.X;
                screen.UIBounds = uiBounds;
                screen.SwapTypeBounds = new RectangleF(uiBounds.X, uiBounds.Y, uiBounds.Width * 0.1f, uiBounds.Width * 0.1f);

                screen.Index = i;
                GetScreenDivisionBounds(screen);
            }

            float instHeight = screensArea.Width / 1.77f;

            parent.instructionImg.Location = new Point((int)screensArea.X, (int)screensArea.Y / 4);
            parent.instructionImg.Size = new Size((int)screensArea.Width, (int)instHeight);
        }

        internal static void GetScreenDivisionBounds(UserScreen screen)
        {
            screen.SubScreensBounds = new Dictionary<Rectangle, RectangleF>();

            UserScreenType screenType = screen.Type;
            Rectangle bounds = screen.MonitorBounds;
            RectangleF ebounds = screen.UIBounds;

            int index = 0;
            int horLines = 0;
            int verLines = 0;
            int maxPlayers = 0;

            bool Regular(int width, int height)
            {
                if (index == maxPlayers)
                {
                    return false;
                }

                int y = index % height;
                int x = (index - y) / height;

                int halfw = bounds.Width / height;
                int halfh = bounds.Height / width;

                Rectangle monitorBounds = new Rectangle(bounds.X + (halfw * y), bounds.Y + (halfh * x), halfw, halfh);
                float ey = (float)index % (float)height;
                float ex = ((float)index - ey) / (float)height;

                float halfwe = ebounds.Width / (float)height;
                float halfhe = ebounds.Height / (float)width;
                RectangleF editorBounds = new RectangleF(ebounds.X + (halfwe * ey), ebounds.Y + (halfhe * ex), halfwe, halfhe);

                screen.SubScreensBounds.Add(monitorBounds, editorBounds);
                return true;
            }

            switch (screenType)
            {
                case UserScreenType.FullScreen:
                    {
                        screen.SubScreensBounds.Add(bounds, ebounds);
                        return;
                    }
                case UserScreenType.DualHorizontal:
                    {
                        horLines = 2;
                        verLines = 1;
                        maxPlayers = 2;

                        break;
                    }
                case UserScreenType.DualVertical:
                    {
                        horLines = 1;
                        verLines = 2;
                        maxPlayers = 2;
                        break;
                    }
                case UserScreenType.FourPlayers:
                    {
                        horLines = 2;
                        verLines = 2;
                        maxPlayers = 4;
                        break;
                    }
                case UserScreenType.SixPlayers:
                    {
                        horLines = 2;
                        verLines = 3;
                        maxPlayers = 6;
                        break;
                    }
                case UserScreenType.EightPlayers:
                    {
                        horLines = 2;
                        verLines = 4;
                        maxPlayers = 8;
                        break;
                    }
                case UserScreenType.SixteenPlayers:
                    {
                        horLines = 4;
                        verLines = 4;
                        maxPlayers = 16;
                        break;
                    }
                case UserScreenType.Custom:
                    {
                        horLines = GameProfile.CustomLayout_Hor + 1;
                        verLines = GameProfile.CustomLayout_Ver + 1;
                        maxPlayers = horLines * verLines;
                        break;
                    }
                case UserScreenType.Manual:
                    {
                        int max = 60;//set grid density, could be an option?
                        int[] divs = new int[max];
                        int last = 0;
                        int destBoundsScaleFactor = 1;

                        float divW;
                        float divH;

                        float width = (float)bounds.Width;
                        float height = (float)bounds.Height;

                        for (float i = 2; i < max; i++)
                        {
                            divW = width / i;
                            divH = height / i;

                            if ((divW % 1) == 0 && (divH % 1) == 0)
                            {
                                divs[(int)i] = (int)i;
                                last = (int)i;
                                if (i < 5)
                                {
                                    destBoundsScaleFactor = (int)i;
                                }
                            }
                        }

                        horLines = (int)divs[last];
                        verLines = (int)divs[last];
                        maxPlayers = horLines * verLines;

                        destBoundsScale = destBoundsScaleFactor;

                        break;
                    }
            }

            while (Regular(horLines, verLines))
            {
                ++index;
            }
        }

        public static Point startPoint;
        public static RectangleF ActiveSizer => activeSizer;
        private static RectangleF activeSizer;

        public static RectangleF GetActiveSizer(MouseEventArgs e)
        {
            if (sizerBtnLeft.Contains(e.Location)) return sizerBtnLeft;
            if (sizerBtnRight.Contains(e.Location)) return sizerBtnRight;
            if (sizerBtnTop.Contains(e.Location)) return sizerBtnTop;
            if (sizerBtnBottom.Contains(e.Location)) return sizerBtnBottom;

            return RectangleF.Empty;
        }


        internal static void OnMouseDown(MouseEventArgs e)
        {
            List<PlayerInfo> players = profile.DevicesList;

            if (dragging)
            {
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                activeSizer = GetActiveSizer(e);

                if (activeSizer != RectangleF.Empty)
                {
                    Cursor.Position = parent.PointToScreen(new Point((int)(parent.Location.X + activeSizer.X + activeSizer.Width / 2), (int)(parent.Location.Y + activeSizer.Y + activeSizer.Height / 2)));
                    return;
                }
                else
                {
                    Cursor.Current = Draw.hand_Cursor;
                }

                for (int i = 0; i < screens.Length; i++)
                {
                    UserScreen screen = screens[i];

                    if (screen.SwapTypeBounds.Contains(e.Location))
                    {
                        if (GameProfile.Loaded)
                        {
                            return;
                        }

                        if (screen.Type == UserScreenType.Manual)
                        {
                            screen.Type = 0;
                        }
                        else
                        {
                            screen.Type++;
                        }

                        GetScreenDivisionBounds(screen);

                        ///invalidate all players inside screen
                        for (int j = 0; j < players.Count; j++)
                        {
                            ///return to default position
                            PlayerInfo p = players[j];

                            if (p.ScreenIndex == i)
                            {
                                RemovePlayer(p, i);
                            }
                        }

                        DevicesFunctions.UpdateDevices();
                        parent.Invalidate();
                        return;
                    }
                }

                for (int i = 0; i < players.Count; i++)
                {
                    PlayerInfo p = players[i];

                    RectangleF r = p.EditBounds;

                    if (r.Contains(e.Location))
                    {
                        if (!p.IsInputUsed)
                        {
                            return;
                        }

                        if (p.ScreenIndex != -1)
                        {
                            RemovePlayer(p, p.ScreenIndex);
                        }

                        dragging = true;
                        draggingIndex = i;
                        //Cursor.Position = Parent.PointToScreen(new Point((int)(Parent.Location.X + r.X + r.Width / 2), (int)(Parent.Location.Y + r.Y + r.Height / 2)));
                        draggingOffset = new PointF(r.X - e.X, r.Y - e.Y);

                        RectangleF newBounds = GetDefaultBounds(draggingIndex);

                        profile.DevicesList[draggingIndex].EditBounds = newBounds;

                        if (draggingOffset.X < -newBounds.Width ||
                            draggingOffset.Y < -newBounds.Height)
                        {
                            draggingOffset = new Point(0, 0);
                        }

                        break;
                    }
                }
            }
            else if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
            {
                for (int i = 0; i < screens.Length; i++)
                {
                    UserScreen screen = screens[i];

                    if (screen.SwapTypeBounds.Contains(e.Location))
                    {
                        if (GameProfile.Loaded)
                        {
                            return;
                        }

                        if (screen.Type == UserScreenType.FullScreen)
                        {
                            screen.Type = UserScreenType.Manual;
                        }
                        else
                        {
                            screen.Type--;
                        }

                        GetScreenDivisionBounds(screen);

                        ///invalidate all players inside screen
                        for (int j = 0; j < players.Count; j++)
                        {
                            ///return to default position
                            PlayerInfo p = players[j];

                            if (p.ScreenIndex == i)
                            {
                                RemovePlayer(p, p.ScreenIndex);
                            }
                        }

                        parent.Invalidate();
                        return;
                    }
                }

                if (GameProfile.Loaded)
                {
                    return;
                }

                ///if over a player on a screen, expand player bounds
                for (int i = 0; i < players.Count; i++)
                {
                    PlayerInfo p = players[i];

                    RectangleF r = p.EditBounds;
                    RectangleF pib = Rectangle.Empty;

                    PlayerInfo playerInbounds = players.Where(pl => pl != p && pl.EditBounds == r).FirstOrDefault();

                    if (playerInbounds != null)
                    {
                        pib = playerInbounds.EditBounds;
                    }

                    if (r.Contains(e.Location) || pib.Contains(e.Location))
                    {
                        if (p.ScreenIndex != -1)
                        {
                            UserScreen screen = screens[p.ScreenIndex];

                            int verLines = 2;
                            int horLines = 2;

                            switch (screen.Type)
                            {
                                case UserScreenType.FourPlayers:
                                    {
                                        verLines = 2;
                                        horLines = 2;
                                    }
                                    break;
                                case UserScreenType.SixPlayers:
                                    {
                                        verLines = 3;
                                        horLines = 2;
                                    }
                                    break;
                                case UserScreenType.EightPlayers:
                                    {
                                        verLines = 4;
                                        horLines = 2;
                                    }
                                    break;
                                case UserScreenType.SixteenPlayers:
                                    {
                                        verLines = 4;
                                        horLines = 4;
                                    }
                                    break;
                                case UserScreenType.Custom:
                                    {
                                        horLines = GameProfile.CustomLayout_Hor + 1;
                                        verLines = GameProfile.CustomLayout_Ver + 1;
                                    }
                                    break;
                                case UserScreenType.Manual:
                                    return;
                            }

                            int halfWidth = screen.MonitorBounds.Width / verLines;
                            int halfHeight = screen.MonitorBounds.Height / horLines;

                            Rectangle bounds = p.MonitorBounds;
                            if ((int)screen.Type >= 3)
                            {
                                ///check if the size is 1/4th of screen
                                if (bounds.Width == halfWidth && bounds.Height == halfHeight)
                                {
                                    bool hasLeftRightSpace = true;
                                    bool hasTopBottomSpace = true;

                                    if (players.Where(pl => (pl != p && pl.ScreenIndex != -1) && (pl.EditBounds.Left == r.Right || (pl.EditBounds.Right == r.Left && r.Right == screen.UIBounds.Right)) && (pl.EditBounds.Y == r.Y)).Count() > 0)
                                    {
                                        hasLeftRightSpace = false;
                                    }

                                    if (players.Where(pl => (pl != p && pl.ScreenIndex != -1) && (pl.EditBounds.Top == r.Bottom || (pl.EditBounds.Top == r.Bottom && r.Bottom == screen.UIBounds.Bottom)) && pl.EditBounds.X == r.X).Count() > 0)
                                    {
                                        hasTopBottomSpace = false;
                                    }

                                    ///check if we have something left/right or top/bottom
                                    if (hasLeftRightSpace)
                                    {
                                        RectangleF edit = r;

                                        if (edit.X > screen.UIBounds.X + edit.Width)
                                        {
                                            bounds.X -= bounds.Width;
                                            edit.X -= edit.Width;
                                        }

                                        bounds.Width *= verLines;
                                        edit.Width *= verLines;

                                        while (players.Where(pl => (pl != p) && (pl.ScreenIndex != -1) && bounds.IntersectsWith(pl.MonitorBounds) && pl.EditBounds != r).Count() > 0 ||
                                            edit.Right > screen.UIBounds.Right)
                                        {
                                            bounds.Width -= p.MonitorBounds.Width;
                                            edit.Width -= p.EditBounds.Width;
                                        }

                                        if (edit != r)
                                        {
                                            p.EditBounds = edit;
                                            p.MonitorBounds = bounds;

                                            if (playerInbounds != null)
                                            {
                                                playerInbounds.MonitorBounds = p.MonitorBounds;
                                                playerInbounds.EditBounds = p.EditBounds;
                                            }

                                            parent.Invalidate();
                                            break;
                                        }

                                    }

                                    if (hasTopBottomSpace)
                                    {
                                        RectangleF edit = r;

                                        if (edit.Y > screen.UIBounds.Y + edit.Height)
                                        {
                                            bounds.Y -= bounds.Height;
                                            edit.Y -= edit.Height;
                                        }

                                        bounds.Height *= horLines;
                                        edit.Height *= horLines;

                                        while (players.Where(pl => (pl != p) && (pl.ScreenIndex != -1) && bounds.IntersectsWith(pl.MonitorBounds) && pl.EditBounds != r).Count() > 0 ||
                                            edit.Bottom > screen.UIBounds.Bottom)
                                        {
                                            bounds.Height -= p.MonitorBounds.Height;
                                            edit.Height -= r.Height;
                                        }

                                        p.EditBounds = edit;
                                        p.MonitorBounds = bounds;

                                        if (playerInbounds != null)
                                        {
                                            playerInbounds.MonitorBounds = p.MonitorBounds;
                                            playerInbounds.EditBounds = p.EditBounds;
                                        }

                                        parent.Invalidate();
                                        break;
                                    }
                                }
                                else
                                {
                                    bounds.Width = screen.MonitorBounds.Width / verLines;
                                    bounds.Height = screen.MonitorBounds.Height / horLines;
                                    p.MonitorBounds = bounds;

                                    RectangleF edit = p.EditBounds;
                                    edit.Width = screen.UIBounds.Width / verLines;
                                    edit.Height = screen.UIBounds.Height / horLines;
                                    p.EditBounds = edit;

                                    if (playerInbounds != null)
                                    {
                                        playerInbounds.MonitorBounds = p.MonitorBounds;
                                        playerInbounds.EditBounds = p.EditBounds;
                                    }

                                    parent.Invalidate();
                                }
                            }
                        }
                    }
                }
            }
        }


        internal static void AddPlayer(PlayerInfo player, int screenIndex)
        {
            if ((player.IsRawKeyboard || player.IsRawMouse) && !(player.IsRawKeyboard && player.IsRawMouse))
            {
                if (GameProfile.loadedProfilePlayers.Any(pl => pl.MonitorBounds == destMonitorBounds))
                {
                    GameProfile.devicesToMerge.Add(player);
                }
                else
                {
                    GameProfile.loadedProfilePlayers.Add(player);
                }

                if (profile.DevicesList.Where(pp => pp.MonitorBounds == destMonitorBounds).Count() == 1)
                {
                    screens[screenIndex].PlayerOnScreen++;
                    GameProfile.TotalAssignedPlayers++;
                }
            }
            else
            {
                GameProfile.loadedProfilePlayers.Add(player);
                screens[screenIndex].PlayerOnScreen++;
                GameProfile.TotalAssignedPlayers++;
            }

            player.IsInputUsed = true;
            player.Owner = screens[screenIndex];
            player.ScreenIndex = screenIndex;

            player.MonitorBounds = destMonitorBounds;
            player.EditBounds = destEditBounds;

            player.ScreenPriority = screens[screenIndex].priority;
            player.DisplayIndex = screens[screenIndex].DisplayIndex;
        }


        internal static void RemovePlayer(PlayerInfo player, int screenIndex)
        {
            int playerIndex = profile.DevicesList.FindIndex(p => p == player);

            if ((player.IsRawKeyboard || player.IsRawMouse) && !(player.IsRawKeyboard && player.IsRawMouse))
            {
                if (GameProfile.loadedProfilePlayers.Contains(player))
                {
                    GameProfile.loadedProfilePlayers.Remove(player);
                }
                else if (GameProfile.devicesToMerge.Contains(player))
                {
                    GameProfile.devicesToMerge.Remove(player);
                }

                if (profile.DevicesList.Where(pp => pp.MonitorBounds == player.MonitorBounds).Count() == 2)
                {
                    screens[screenIndex].PlayerOnScreen--;
                    GameProfile.TotalAssignedPlayers--;
                }
            }
            else
            {
                GameProfile.loadedProfilePlayers.Remove(player);
                screens[screenIndex].PlayerOnScreen--;
                GameProfile.TotalAssignedPlayers--;
            }

            if (player.ScreenIndex == screenIndex)
            {
                player.EditBounds = GetDefaultBounds(playerIndex);
                player.Owner = null;
                player.ScreenIndex = -1;
                player.MonitorBounds = Rectangle.Empty;
                player.DisplayIndex = -1;
            }
        }


        internal static void OnMouseUp(MouseEventArgs e)
        {
            if (activeSizer == RectangleF.Empty)
            {
                parent.Cursor = Draw.default_Cursor;
            }

            if (e.Button == MouseButtons.Left)
            {
                if (dragging)
                {
                    dragging = false;

                    PlayerInfo p = profile.DevicesList[draggingIndex];

                    if (draggingScreen != -1)
                    {
                        AddPlayer(p, draggingScreen);
                        draggingScreen = -1;
                    }
                    else
                    {
                        for (int i = 0; i < screens.Length; i++)
                        {
                            if (p.ScreenIndex == i)
                            {
                                // return to default position
                                p.Owner = null;
                                p.EditBounds = GetDefaultBounds(draggingIndex);
                                p.MonitorBounds = Rectangle.Empty;
                                p.ScreenPriority = -1;
                                p.ScreenIndex = -1;
                            }
                        }
                    }

                    DevicesFunctions.UpdateDevices();
                    parent.Invalidate();
                }

                activeSizer = RectangleF.Empty;
            }
        }

        private static Point mousePos;

        internal static void OnMouseMove(MouseEventArgs e)
        {
            mousePos = e.Location;

            if (dragging)
            {
                List<PlayerInfo> players = profile.DevicesList;

                PlayerInfo player = players[draggingIndex];

                if (!player.IsInputUsed)
                {
                    return;
                }

                player.MonitorBounds = Rectangle.Empty;

                UserScreen screen = screens.Where(scr => scr.UIBounds.Contains(e.Location)).FirstOrDefault();

                if (screen != null)
                {
                    RectangleF s = screen.UIBounds;

                    float offset = s.Width * 0.05f;

                    draggingScreen = screen.Index;

                    if (!GetFreeSpace(player))
                    {
                        draggingScreen = -1;
                    }
                    else
                    {
                        player.EditBounds = destEditBounds;
                        parent.Invalidate();
                        return;
                    }
                }
                else
                {
                    draggingScreen = -1;
                    destEditBounds = RectangleF.Empty;
                }

                RectangleF p = new RectangleF(mousePos.X + draggingOffset.X, mousePos.Y + draggingOffset.Y, player.SourceEditBounds.Width, player.SourceEditBounds.Height);
                player.EditBounds = p;

                parent.Invalidate();
            }
            else
            {
                selectedPlayer = profile.DevicesList.Where(pl => pl.ScreenIndex != -1 && pl.EditBounds.Contains(e.Location)).FirstOrDefault();

                if (selectedPlayer != null)
                {
                    int maxPlayers = screens[selectedPlayer.ScreenIndex].SubScreensBounds.Count();

                    if (maxPlayers >= 4)
                    {
                        if (!GameProfile.Loaded)
                        {
                            sizer = selectedPlayer.EditBounds;
                            UpdatetSizersBounds();
                            SetCursor(e.Location);

                            if (activeSizer != RectangleF.Empty)
                            {
                                EditPlayerBounds(e);
                            }
                        }
                        else
                        {
                            sizer = RectangleF.Empty;
                        }

                        return;
                    }
                    else
                    {
                        sizer = RectangleF.Empty;
                        return;
                    }
                }
                else
                {
                    sizer = RectangleF.Empty;
                }

                if (parent.Cursor != Draw.default_Cursor)
                {
                    parent.Cursor = Draw.default_Cursor;
                }
            }
        }


        internal static void SetCursor(Point cursorLoc)
        {
            if (sizerBtnLeft.Contains(cursorLoc) || sizerBtnRight.Contains(cursorLoc))
            {
                parent.Cursor = Cursors.SizeWE;
            }
            else if (sizerBtnTop.Contains(cursorLoc) || sizerBtnBottom.Contains(cursorLoc))
            {
                parent.Cursor = Cursors.SizeNS;
            }
            else
            {
                parent.Cursor = Draw.hand_Cursor;
            }
        }

        internal static bool GetFreeSpace(PlayerInfo player)
        {
            if (draggingScreen == -1)
            {
                return false;
            }

            List<PlayerInfo> players = profile.DevicesList;
      
            UserScreen screen = screens[draggingScreen];
            RectangleF s = screen.UIBounds;

            destMonitorBounds = screen.SubScreensBounds.Where(b => b.Value.Contains(mousePos)).FirstOrDefault().Key;
            destEditBounds = screen.SubScreensBounds.Where(b => b.Value.Contains(mousePos)).FirstOrDefault().Value;

            if (screen.Type == UserScreenType.Manual)
            {
                destMonitorBounds = new Rectangle(destMonitorBounds.X, destMonitorBounds.Y, screen.MonitorBounds.Width / destBoundsScale, screen.MonitorBounds.Height / destBoundsScale);
                destEditBounds = new RectangleF(destEditBounds.X, destEditBounds.Y, screen.UIBounds.Width / (float)destBoundsScale, screen.UIBounds.Height / (float)destBoundsScale);

                if (destEditBounds.Bottom > s.Bottom || destEditBounds.Right > s.Right)
                {
                    return false;
                }
            }

            var playersInDiv = players.Where(pl => (pl != player) && pl.MonitorBounds.IntersectsWith(destMonitorBounds)).ToList();

            if (player.IsRawMouse && !(player.IsRawMouse && player.IsRawKeyboard) ? playersInDiv.All(x => x.IsRawKeyboard && !x.IsRawMouse) :
               player.IsRawKeyboard && !(player.IsRawMouse && player.IsRawKeyboard) ? playersInDiv.All(x => x.IsRawMouse && !x.IsRawKeyboard) :
                !playersInDiv.Any())
            {
                if (GameProfile.Loaded)
                {
                    if (GameProfile.ProfilePlayersList.Count == 0)
                    {
                        return false;
                    }

                    //Check if this bounds are the bounds of a profile player. 
                    ProfilePlayer profilePlayer = GameProfile.ProfilePlayersList.Where(ppl => GameProfile.TranslateBounds(ppl, GameProfile.FindScreenOrAlternative(ppl).Item1).Item1.IntersectsWith(destMonitorBounds)).FirstOrDefault();

                    if (GameProfile.TotalAssignedPlayers == GameProfile.TotalProfilePlayers || profilePlayer == null)
                    {
                        return false;
                    }

                    var translatedBounds = GameProfile.TranslateBounds(profilePlayer, GameProfile.FindScreenOrAlternative(profilePlayer).Item1);

                    destMonitorBounds = translatedBounds.Item1;
                    destEditBounds = translatedBounds.Item2;

                    return true;
                }

                if (playersInDiv.Count() > 0)
                {
                    destMonitorBounds = playersInDiv.First().MonitorBounds;
                    destEditBounds = playersInDiv.First().EditBounds;
                }

                return true;
            }

            return false;
        }


        internal static void UpdatetSizersBounds()
        {
            sizerBtnLeft = new RectangleF(sizer.Left, sizer.Top + (sizer.Height / 3), sizer.Width / 3, sizer.Height / 3);
            sizerBtnRight = new RectangleF(sizer.Right - sizer.Width / 3, sizer.Top + (sizer.Height / 3), sizer.Width / 3, sizer.Height / 3);
            sizerBtnTop = new RectangleF(sizer.Left + (sizer.Width / 3), sizer.Top, (sizer.Width / 3), (sizer.Height / 3));
            sizerBtnBottom = new RectangleF(sizer.Left + (sizer.Width / 3), sizer.Bottom - (sizer.Height / 3), (sizer.Width / 3), (sizer.Height / 3));
            sizerBtnCenter = new RectangleF(sizerBtnLeft.Right, sizerBtnTop.Bottom, sizerBtnRight.Left - sizerBtnLeft.Right, sizerBtnBottom.Top - sizerBtnTop.Bottom);
        }


        internal static void EditPlayerBounds(MouseEventArgs e)
        {
            if (selectedPlayer != null && !GameProfile.Loaded)
            {
                List<PlayerInfo> players = profile.DevicesList;

                PlayerInfo p = selectedPlayer;

                if (p.ScreenIndex != -1)
                {
                    UserScreen screen = screens[p.ScreenIndex];

                    bool isManual = screen.Type == UserScreenType.Manual;

                    Rectangle pmb = p.MonitorBounds;
                    RectangleF peb = p.EditBounds;
                    RectangleF pmbToCompare = pmb;

                    Size mSubScreen = new Size(screen.SubScreensBounds.ElementAt(0).Key.Width, screen.SubScreensBounds.ElementAt(0).Key.Height);
                    SizeF eSubScreen = new SizeF(screen.SubScreensBounds.ElementAt(0).Value.Width, screen.SubScreensBounds.ElementAt(0).Value.Height);

                    PlayerInfo playerInbounds = players.Where(pl => (pl != p) && pl.MonitorBounds == pmb).FirstOrDefault();

                    float offset = 2;//(isManual || screen.SubScreensBounds.Count() > 10) ? 2 : 5;//Can be very sensitive with few sub screens

                    if (activeSizer == sizerBtnLeft)
                    {
                        if (e.Location.X <= (activeSizer.Left + (activeSizer.Width / 2)) - offset)
                        {
                            if (pmb.Left != screen.MonitorBounds.Left && players.Where(pl => (pl != p) && pl.MonitorBounds.Right == pmb.Left && pl.MonitorBounds.Y == pmb.Y && pl != playerInbounds).Count() == 0)
                            {
                                PlayerInfo other = players.Where(pl => (pl != p && pl.ScreenIndex != -1) && pl.MonitorBounds != p.MonitorBounds && pl.MonitorBounds.Right < pmb.Left && pl.MonitorBounds.Y == pmb.Y && pl != playerInbounds).FirstOrDefault();

                                pmb.Width += mSubScreen.Width;
                                peb.Width += eSubScreen.Width;

                                pmb.Location = new Point(pmb.X - mSubScreen.Width, pmb.Y);
                                peb.Location = new PointF(peb.X - eSubScreen.Width, peb.Y);

                                int mboundsLimit = other == null ? screen.MonitorBounds.X : other.MonitorBounds.Right;
                                float eboundsLimit = other == null ? screen.UIBounds.X : other.EditBounds.Right;

                                if (pmb.Left >= mboundsLimit && players.Where(pl => (pl != p) && pl.MonitorBounds.IntersectsWith(pmb) && pl != playerInbounds).Count() == 0)
                                {
                                    if ((screen.MonitorBounds.Right - pmb.Right) < mSubScreen.Width && Math.Abs(mboundsLimit - pmb.Left) < mSubScreen.Width && other == null)
                                    {
                                        pmb.Width = screen.MonitorBounds.Width;
                                        pmb.X = screen.MonitorBounds.X;
                                    }

                                    p.MonitorBounds = pmb;
                                    p.EditBounds = peb;

                                    if (playerInbounds != null)
                                    {
                                        playerInbounds.MonitorBounds = pmb;
                                        playerInbounds.EditBounds = peb;
                                    }

                                    sizer = p.EditBounds;
                                    UpdatetSizersBounds();
                                    activeSizer = sizerBtnLeft;
                                    Cursor.Position = parent.PointToScreen(new Point((int)(parent.Location.X + activeSizer.X + activeSizer.Width / 2), (int)(parent.Location.Y + activeSizer.Y + activeSizer.Height / 2)));
                                }
                            }

                        }
                        else if (e.Location.X > (activeSizer.Right - (activeSizer.Width / 2)) + offset)
                        {
                            if (pmb.Width >= mSubScreen.Width)
                            {
                                pmb.Width -= mSubScreen.Width;
                                peb.Width -= eSubScreen.Width;

                                if (isManual)
                                {
                                    if (pmb.Width < screen.MonitorBounds.Width / destBoundsScale)
                                    {
                                        return;
                                    }
                                }
                                else
                                {
                                    if (pmb.Width < mSubScreen.Width)
                                    {
                                        return;
                                    }
                                }

                                pmb.Location = new Point(pmb.X + mSubScreen.Width, pmb.Y);
                                peb.Location = new PointF(peb.X + eSubScreen.Width, peb.Y);

                                p.MonitorBounds = pmb;
                                p.EditBounds = peb;

                                if (playerInbounds != null)
                                {
                                    playerInbounds.MonitorBounds = pmb;
                                    playerInbounds.EditBounds = peb;
                                }

                                sizer = p.EditBounds;
                                sizerBtnLeft = new RectangleF(sizer.Left, sizer.Top + (sizer.Height / 3), sizer.Width / 3, sizer.Height / 3);
                                activeSizer = sizerBtnLeft;
                                Cursor.Position = parent.PointToScreen(new Point((int)(parent.Location.X + activeSizer.X + activeSizer.Width / 2), (int)(parent.Location.Y + activeSizer.Y + activeSizer.Height / 2)));
                            }
                        }
                    }
                    else if (activeSizer == sizerBtnRight)
                    {
                        if (e.Location.X > (activeSizer.Right - (activeSizer.Width / 2)) + offset)
                        {
                            if (pmb.Right != screen.MonitorBounds.Right && players.Where(pl => (pl != p) && pl.MonitorBounds.Left == pmb.Right && pl.MonitorBounds.Y == pmb.Y && pl != playerInbounds).Count() == 0)
                            {
                                PlayerInfo other = players.Where(pl => (pl != p && pl.ScreenIndex != -1) && pl.MonitorBounds != p.MonitorBounds && pl.MonitorBounds.Left > pmb.Right && pl.MonitorBounds.Y == pmb.Y && pl != playerInbounds).FirstOrDefault();

                                pmb.Width += mSubScreen.Width;
                                peb.Width += eSubScreen.Width;

                                int mboundsLimit = other == null ? screen.MonitorBounds.Right : other.MonitorBounds.Left;
                                float eboundsLimit = other == null ? screen.UIBounds.Right : other.EditBounds.Left;

                                if (pmb.Right <= mboundsLimit && players.Where(pl => (pl != p) && pl.MonitorBounds.IntersectsWith(pmb) && pl != playerInbounds).Count() == 0)
                                {
                                    if (pmb.Left == screen.MonitorBounds.Left && Math.Abs(mboundsLimit - pmb.Right) < mSubScreen.Width && other == null)
                                    {
                                        pmb.Width = screen.MonitorBounds.Width;
                                        pmb.X = screen.MonitorBounds.X;
                                    }

                                    p.MonitorBounds = pmb;
                                    p.EditBounds = peb;

                                    if (playerInbounds != null)
                                    {
                                        playerInbounds.MonitorBounds = pmb;
                                        playerInbounds.EditBounds = peb;
                                    }

                                    sizer = p.EditBounds;
                                    UpdatetSizersBounds();
                                    activeSizer = sizerBtnRight;
                                    Cursor.Position = parent.PointToScreen(new Point((int)(parent.Location.X + activeSizer.X + activeSizer.Width / 2), (int)(parent.Location.Y + activeSizer.Y + activeSizer.Height / 2)));
                                }
                            }
                        }
                        else if (e.Location.X <= (activeSizer.Left + (activeSizer.Width / 2)) - offset)
                        {
                            if (pmb.Width > mSubScreen.Width)
                            {
                                pmb.Width -= mSubScreen.Width;
                                peb.Width -= eSubScreen.Width;

                                if (isManual)
                                {
                                    if (pmb.Width < screen.MonitorBounds.Width / destBoundsScale)
                                    {
                                        return;
                                    }
                                }
                                else
                                {
                                    if (pmb.Width < mSubScreen.Width)
                                    {
                                        return;
                                    }
                                }

                                p.MonitorBounds = pmb;
                                p.EditBounds = peb;

                                if (playerInbounds != null)
                                {
                                    playerInbounds.MonitorBounds = pmb;
                                    playerInbounds.EditBounds = peb;
                                }

                                sizer = p.EditBounds;
                                UpdatetSizersBounds();
                                activeSizer = sizerBtnRight;
                                Cursor.Position = parent.PointToScreen(new Point((int)(parent.Location.X + activeSizer.X + activeSizer.Width / 2), (int)(parent.Location.Y + activeSizer.Y + activeSizer.Height / 2)));
                            }
                        }
                    }
                    else if (activeSizer == sizerBtnTop)
                    {
                        if (e.Location.Y <= (activeSizer.Top + (activeSizer.Height / 2)) - offset)
                        {
                            if (pmb.Top != screen.MonitorBounds.Top && players.Where(pl => (pl != p) && pl.MonitorBounds.Bottom == pmb.Top && pl.MonitorBounds.X == pmb.X && pl != playerInbounds).Count() == 0)
                            {
                                PlayerInfo other = players.Where(pl => (pl != p && pl.ScreenIndex != -1) && pl.MonitorBounds != p.MonitorBounds && pl.MonitorBounds.Bottom > pmb.Top && pl.MonitorBounds.X == pmb.X && pl.MonitorBounds.Top != pmb.Bottom && pl != playerInbounds).FirstOrDefault();

                                pmb.Height += mSubScreen.Height;
                                peb.Height += eSubScreen.Height;

                                pmb.Location = new Point(pmb.X, pmb.Y - mSubScreen.Height);
                                peb.Location = new PointF(peb.X, peb.Y - eSubScreen.Height);

                                int mboundsLimit = other == null ? screen.MonitorBounds.Top : other.MonitorBounds.Bottom;
                                float eboundsLimit = other == null ? screen.UIBounds.Top : other.EditBounds.Bottom;

                                if (Math.Abs(p.MonitorBounds.Top) != Math.Abs(mboundsLimit) && players.Where(pl => (pl != p) && pl.MonitorBounds.IntersectsWith(pmb) && pl != playerInbounds).Count() == 0)
                                {
                                    if ((Math.Abs(screen.MonitorBounds.Height) - Math.Abs(pmb.Height)) < mSubScreen.Height && Math.Abs(screen.MonitorBounds.Top - pmb.Top) < mSubScreen.Height && other == null)
                                    {
                                        pmb.Height = screen.MonitorBounds.Height;
                                        pmb.Y = screen.MonitorBounds.Y;
                                    }

                                    p.MonitorBounds = pmb;
                                    p.EditBounds = peb;

                                    if (playerInbounds != null)
                                    {
                                        playerInbounds.MonitorBounds = pmb;
                                        playerInbounds.EditBounds = peb;
                                    }
                                }

                                sizer = p.EditBounds;
                                UpdatetSizersBounds();
                                activeSizer = sizerBtnTop;
                                Cursor.Position = parent.PointToScreen(new Point((int)(parent.Location.X + activeSizer.X + activeSizer.Width / 2), (int)(parent.Location.Y + activeSizer.Y + activeSizer.Height / 2)));
                            }
                        }
                        else if (e.Location.Y >= (activeSizer.Top + (activeSizer.Height / 2)) + offset)
                        {
                            if (pmb.Height > mSubScreen.Height)
                            {
                                pmb.Height -= mSubScreen.Height;
                                peb.Height -= eSubScreen.Height;

                                if (isManual)
                                {
                                    if (pmb.Height < screen.MonitorBounds.Height / destBoundsScale)
                                    {
                                        return;
                                    }
                                }
                                else
                                {
                                    if (pmb.Height < mSubScreen.Height)
                                    {
                                        return;
                                    }
                                }

                                pmb.Location = new Point(pmb.X, pmb.Y + mSubScreen.Height);
                                peb.Location = new PointF(peb.X, peb.Y + eSubScreen.Height);

                                p.MonitorBounds = pmb;
                                p.EditBounds = peb;

                                if (playerInbounds != null)
                                {
                                    playerInbounds.MonitorBounds = pmb;
                                    playerInbounds.EditBounds = peb;
                                }

                                sizer = p.EditBounds;
                                UpdatetSizersBounds();
                                activeSizer = sizerBtnTop;
                                Cursor.Position = parent.PointToScreen(new Point((int)(parent.Location.X + activeSizer.X + activeSizer.Width / 2), (int)(parent.Location.Y + activeSizer.Y + activeSizer.Height / 2)));
                            }
                        }
                    }
                    else if (activeSizer == sizerBtnBottom)
                    {
                        if (e.Location.Y >= (activeSizer.Top + (activeSizer.Height / 2)) + offset)
                        {
                            if (pmb.Bottom <= screen.MonitorBounds.Bottom && players.Where(pl => (pl != p) && pl.MonitorBounds.Top == pmb.Bottom && pl.MonitorBounds.X == pmb.X && pl != playerInbounds).Count() == 0)
                            {
                                PlayerInfo other = players.Where(pl => (pl != p && pl.ScreenIndex != -1) && pl.MonitorBounds != p.MonitorBounds && pl.MonitorBounds.Top > pmb.Bottom && pl.MonitorBounds.X == pmb.X && pl.MonitorBounds.Bottom != pmb.Bottom && pl != playerInbounds).FirstOrDefault();

                                pmb.Height += mSubScreen.Height;
                                peb.Height += eSubScreen.Height;

                                int mboundsLimit = other == null ? screen.MonitorBounds.Bottom : other.MonitorBounds.Top;
                                float eboundsLimit = other == null ? screen.UIBounds.Bottom : other.EditBounds.Top;

                                if (pmb.Bottom <= mboundsLimit && players.Where(pl => (pl != p) && pl.MonitorBounds.IntersectsWith(pmb) && pl != playerInbounds).Count() == 0)
                                {
                                    if (pmb.Top == screen.MonitorBounds.Y && Math.Abs(mboundsLimit - pmb.Bottom) < mSubScreen.Height && other == null)
                                    {
                                        pmb.Height = screen.MonitorBounds.Height;
                                        pmb.Y = screen.MonitorBounds.Y;
                                    }

                                    p.MonitorBounds = pmb;
                                    p.EditBounds = peb;

                                    if (playerInbounds != null)
                                    {
                                        playerInbounds.MonitorBounds = pmb;
                                        playerInbounds.EditBounds = peb;
                                    }

                                    sizer = p.EditBounds;
                                    UpdatetSizersBounds();
                                    activeSizer = sizerBtnBottom;
                                    Cursor.Position = parent.PointToScreen(new Point((int)(parent.Location.X + activeSizer.X + activeSizer.Width / 2), (int)(parent.Location.Y + activeSizer.Y + activeSizer.Height / 2)));
                                }
                            }
                        }
                        else if (e.Location.Y <= (activeSizer.Top + (activeSizer.Height / 2)) - offset)
                        {
                            if (pmb.Height > mSubScreen.Height)
                            {
                                pmb.Height -= mSubScreen.Height;
                                peb.Height -= eSubScreen.Height;

                                if (isManual)
                                {
                                    if (pmb.Height < screen.MonitorBounds.Height / destBoundsScale)
                                    {
                                        return;
                                    }
                                }
                                else
                                {
                                    if (pmb.Height < mSubScreen.Height)
                                    {
                                        return;
                                    }
                                }

                                p.MonitorBounds = pmb;
                                p.EditBounds = peb;

                                if (playerInbounds != null)
                                {
                                    playerInbounds.MonitorBounds = pmb;
                                    playerInbounds.EditBounds = peb;
                                }

                                sizer = p.EditBounds;
                                UpdatetSizersBounds();
                                activeSizer = sizerBtnBottom;
                                Cursor.Position = parent.PointToScreen(new Point((int)(parent.Location.X + activeSizer.X + activeSizer.Width / 2), (int)(parent.Location.Y + activeSizer.Y + activeSizer.Height / 2)));
                            }
                        }
                    }

                    parent.Invalidate();
                }
            }
        }


        internal static void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (dragging || GameProfile.Loaded)
            {
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                //first count how many devices we have
                bool changed = false;

                for (int i = 0; i < screens.Length; i++)
                {
                    UserScreen screen = screens[i];

                    if (screen.Type == UserScreenType.Manual)
                    {
                        return;
                    }

                    if (screen.UIBounds.Contains(e.Location) && !screen.SwapTypeBounds.Contains(e.Location))
                    {
                        List<PlayerInfo> players = profile.DevicesList;

                        // add all possible players!
                        for (int p = 0; p < players.Count; p++)
                        {
                            PlayerInfo player = players[p];

                            for (int b = 0; b < screen.SubScreensBounds.Count; b++)
                            {
                                destMonitorBounds = screen.SubScreensBounds.ElementAt(b).Key;
                                destEditBounds = screen.SubScreensBounds.ElementAt(b).Value;

                                if (GetFreeSpace(player))
                                {
                                    if (player.ScreenIndex == -1)
                                    {
                                        changed = true;

                                        AddPlayer(player, i);
                                    }
                                }
                            }
                        }

                    }
                }

                if (changed)
                {
                    DevicesFunctions.UpdateDevices();
                }
            }
        }
    }
}
