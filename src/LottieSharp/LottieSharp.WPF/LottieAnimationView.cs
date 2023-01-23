﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Skottie;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace LottieSharp.WPF
{
    public class LottieAnimationView : SKElement
    {
        private readonly Stopwatch watch = new();
        private Animation animation;
        private DispatcherTimer timer;
        private int loopCount;

        public AnimationInfo Info
        {
            get { return (AnimationInfo)GetValue(InfoProperty); }
            set { SetValue(InfoProperty, value); }
        }

        public event EventHandler OnStop;

        public string FileName
        {
            get => (string)GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        public string ResourcePath
        {
            get => (string)GetValue(ResourcePathProperty);
            set => SetValue(ResourcePathProperty, value);
        }

        public virtual void PlayAnimation()
        {
            timer.Start();
            watch.Start();
            IsPlaying = true;
        }

        public virtual void StopAnimation()
        {
            loopCount = RepeatCount;
            timer.Stop();
            watch.Reset();
            IsPlaying = false;

            OnStop?.Invoke(this, null);
        }

        public int RepeatCount
        {
            get { return (int)GetValue(RepeatCountProperty); }
            set { SetValue(RepeatCountProperty, value); }
        }

        public static readonly DependencyProperty RepeatCountProperty =
            DependencyProperty.Register("RepeatCount", typeof(int), typeof(LottieAnimationView), new PropertyMetadata(0, RepeatCountChangedCallback));

        private static void RepeatCountChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LottieAnimationView lottieAnimationView)
            {
                lottieAnimationView.loopCount = (int)e.NewValue;
            }
        }

        public bool AutoPlay
        {
            get { return (bool)GetValue(AutoStartProperty); }
            set { SetValue(AutoStartProperty, value); }
        }

        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.Register("AutoPlay", typeof(bool), typeof(LottieAnimationView), new PropertyMetadata(false, AutoPlayPropertyChangedCallback));

        public static readonly DependencyProperty InfoProperty =
            DependencyProperty.Register("Info", typeof(AnimationInfo), typeof(LottieAnimationView));

        private static void AutoPlayPropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            // Not in use at the moment
        }

        public bool IsPlaying
        {
            get { return (bool)GetValue(IsPlayingProperty); }
            set { SetValue(IsPlayingProperty, value); }
        }

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register("IsPlaying", typeof(bool), typeof(LottieAnimationView), new PropertyMetadata(false));

        public RepeatMode Repeat
        {
            get { return (RepeatMode)GetValue(RepeatProperty); }
            set { SetValue(RepeatProperty, value); }
        }

        public static readonly DependencyProperty RepeatProperty =
            DependencyProperty.Register("Repeat", typeof(RepeatMode), typeof(LottieAnimationView), new PropertyMetadata(RepeatMode.Restart));

        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register("FileName", typeof(string), typeof(LottieAnimationView), new PropertyMetadata(null, FileNamePropertyChangedCallback));

        public static readonly DependencyProperty ResourcePathProperty =
            DependencyProperty.Register("ResourcePath", typeof(string), typeof(LottieAnimationView), new PropertyMetadata(null, ResourcePathPropertyChangedCallback));

        private static void FileNamePropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is LottieAnimationView lottieAnimationView && e.NewValue is string assetName)
            {
                lottieAnimationView.SetAnimationFromFile(assetName);
            }
        }

        private static void ResourcePathPropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is LottieAnimationView lottieAnimationView && e.NewValue is string assetName)
            {
                lottieAnimationView.SetAnimationFromResource(assetName);
            }
        }

        private void SetAnimationFromFile(string assetName)
        {
            try
            {
                using FileStream stream = File.OpenRead(assetName);
                SetAnimation(stream);
            }
            catch (IOException)
            {
                Debug.WriteLine($"Failed to load {assetName}");
                throw;
            }
            catch (Exception)
            {
                Debug.WriteLine($"Unexpected error when loading {assetName}");
                throw;
            }
        }

        private void SetAnimationFromResource(string assetUri)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }

            try
            {
                var resourceUri = new Uri(assetUri);
                var resourceInfo = Application.GetResourceStream(resourceUri);

                SetAnimation(resourceInfo?.Stream);
            }
            catch (IOException)
            {
                Debug.WriteLine($"Failed to load resource {assetUri}");
                throw;
            }
            catch (UriFormatException)
            {
                Debug.WriteLine($"Resource URI failure for resource {assetUri}");
                throw;
            }
            catch (Exception)
            {
                Debug.WriteLine($"Unexpected error when loading resource {assetUri}");
                throw;
            }

        }

        private void SetAnimation(Stream stream)
        {


            using SKManagedStream fileStream = new(stream);

            if (Animation.TryCreate(fileStream, out animation))
            {
                animation.Seek(0);
                Info = new AnimationInfo(animation.Version, animation.Duration, animation.Fps, animation.InPoint,
                    animation.OutPoint);
            }
            else
            {
                Info = new AnimationInfo(string.Empty, TimeSpan.Zero, 0, 0, 0);
                throw new InvalidOperationException("Failed to load animation");
            }

            watch.Reset();
            if (timer == null)
            {
                timer = new DispatcherTimer(DispatcherPriority.Render);
                timer.Interval = TimeSpan.FromSeconds(Math.Max(1 / 60.0, 1 / animation.Fps));
                timer.Tick += (s, e) => { InvalidateVisual(); };
            }
            else
            {
                timer.Stop();
                timer.Interval = TimeSpan.FromSeconds(Math.Max(1 / 60.0, 1 / animation.Fps));
            }

            if (AutoPlay || IsPlaying)
            {
                PlayAnimation();
            }
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);

            SKCanvas canvas = e.Surface.Canvas;
            canvas.Clear(SKColor.Empty);
            SKImageInfo info = e.Info;

            if (animation != null)
            {
                animation.SeekFrameTime((float)watch.Elapsed.TotalSeconds);

                if (watch.Elapsed.TotalSeconds > animation.Duration.TotalSeconds)
                {
                    if (Repeat == RepeatMode.Restart)
                    {
                        if (RepeatCount == Defaults.RepeatCountInfinite)
                        {
                            watch.Restart();
                        }
                        else if (RepeatCount > 0 && loopCount > 0)
                        {
                            loopCount--;
                            watch.Restart();
                        }
                        else
                        {
                            StopAnimation();
                        }
                    }
                }

                animation.Render(canvas, new SKRect(0, 0, info.Width, info.Height));
            }
        }
    }
}
