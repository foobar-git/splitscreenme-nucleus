﻿
using Nucleus.Gaming;
using Nucleus;
using Nucleus.Gaming.Cache;
using Nucleus.Gaming.Coop;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System;
using System.Windows.Forms;
using System.Threading;

public static class WPFDivFormThread
{
    public static void StartBackgroundForm(GenericGameInfo gen,Display dp)
    {
        Thread backgroundFormThread = new Thread(delegate ()
        {
            WPFDiv backgroundForm = new WPFDiv(gen, dp);
            backgroundForm.Show();
            System.Windows.Threading.Dispatcher.Run();
        });

        backgroundFormThread.SetApartmentState(ApartmentState.STA); // needs to be STA or throws exception
        backgroundFormThread.Start();
    }
}

public class WPFDiv : System.Windows.Window
{
    private System.Windows.Forms.Timer fading;
    private SolidColorBrush userColor;
    private string gameGUID;
    private float alpha = 1.0F;
    private bool fullApha = true;
    private int imgIndex = 0;
    private ImageBrush backBrush;

    public WPFDiv(GenericGameInfo game, Display screen)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;

        Background = Brushes.Black;
        Topmost = false;

        Name = $"SplitForm{screen.DisplayIndex}";

        WindowStartupLocation = WindowStartupLocation.Manual;
     
        Left = screen.Bounds.Left;
        Top = screen.Bounds.Top;

        Width = screen.Bounds.Width;
        Height = screen.Bounds.Height;
        
        backBrush = new ImageBrush();

        gameGUID = game.GUID;
        game.OnFinishedSetup += SetupFinished;

        GenericGameHandler.Instance?.splitForms.Add(this);

        Setup();
    }

    private void Setup()
    {
        IDictionary<string, SolidColorBrush> splitColors = new Dictionary<string, SolidColorBrush>
        {
                { "Black", Brushes.Black },
                { "Gray", Brushes.DimGray },
                { "White", Brushes.White },
                { "Dark Blue", Brushes.DarkBlue },
                { "Blue", Brushes.Blue },
                { "Purple", Brushes.Purple },
                { "Pink", Brushes.Pink },
                { "Red", Brushes.Red },
                { "Orange", Brushes.Orange },
                { "Yellow", Brushes.Yellow },
                { "Green", Brushes.Green }
        };

        foreach (KeyValuePair<string, SolidColorBrush> color in splitColors)
        {
            if (color.Key != GameProfile.SplitDivColor)
            {
                continue;
            }

            userColor = color.Value;
            break;
        }

        SlideshowStart();
    }

    private void SlideshowStart()
    {
        if (fading == null)
        {
            if (Directory.Exists(System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, $@"gui\screenshots\{gameGUID}")))
            {
                string[] imgsPath = Directory.GetFiles((System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, $@"gui\screenshots\{gameGUID}")));

                backBrush.ImageSource = new BitmapImage(new Uri(System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, $@"gui\screenshots\{gameGUID}\{imgIndex}_{gameGUID}.jpeg"), UriKind.Absolute));
                Background = backBrush;
                imgIndex++;
            }

            fading = new System.Windows.Forms.Timer();
            fading.Tick += new EventHandler(FadingTick);
            fading.Interval = 50;
            fading.Start();
        }
    }

    private void FadingTick(object Object, EventArgs EventArgs)
    {
        if (fullApha)
        {
            alpha += 0.01F;
        }

        if (!fullApha)
        {
            alpha -= 0.01F;
        }

        if (alpha <= 0.01F)
        {
            if (Directory.Exists(System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, $@"gui\screenshots\{gameGUID}")))
            {
                string[] imgsPath = Directory.GetFiles((System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, $@"gui\screenshots\{gameGUID}")));

                backBrush.ImageSource = new BitmapImage(new Uri(System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, $@"gui\screenshots\{gameGUID}\{imgIndex}_{gameGUID}.jpeg"), UriKind.Absolute)); //(new UriImageCache.GetImage(System.IO.Path.Combine(System.Windows.Forms.Application.StartupPath, $@"gui\screenshots\{gameGUID}\{imgIndex}_{gameGUID}.jpeg"));
                Background = backBrush;

                imgIndex++;

                if (imgIndex == imgsPath.Length)
                {
                    imgIndex = 0;
                }
            }

            fullApha = true;

        }
        else if (alpha >= 1.0)
        {
            fullApha = false;
        }

        backBrush.Opacity = alpha;
    }

    private void SetupFinished()
    {
        fading.Dispose();
        this.Dispatcher.Invoke(new Action(() => { Background = userColor; }));
    }
}



