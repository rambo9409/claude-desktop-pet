using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ClaudePet
{
    // A reversible clock avoids a new flash when a command changes mid-morph.
    internal sealed class ButterflyMorphState
    {
        internal double Progress { get; private set; }
        internal bool Flying { get; private set; }
        internal double RunUpElapsed { get; private set; }
        internal const double RunUpDuration = 0.48;
        internal bool InTransition { get { return Progress > 0 && Progress < 1; } }
        internal void Aim(bool flying)
        {
            if (flying != Flying)
                // A fresh run has a visible human stride. Reversing an existing
                // dissolution resumes immediately from its current silhouette.
                RunUpElapsed = flying && Progress > 0 ? RunUpDuration : 0;
            Flying = flying;
        }
        internal void Advance(double seconds)
        {
            double elapsed = Math.Max(0, Math.Min(0.05, seconds));
            if (Flying && RunUpElapsed < RunUpDuration)
            {
                double running = Math.Min(elapsed, RunUpDuration - RunUpElapsed);
                RunUpElapsed += running;
                elapsed -= running;
            }
            double step = elapsed / (Flying ? 0.56 : 0.48);
            Progress = Flying ? Math.Min(1, Progress + step) : Math.Max(0, Progress - step);
        }
        internal void Reset() { Progress = 0; Flying = false; RunUpElapsed = 0; }
        internal static double Ease(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return value * value * (3 - 2 * value);
        }
    }

    public sealed partial class PetWindow
    {
        private readonly ButterflyMorphState butterflyMorph = new ButterflyMorphState();
        private Image butterflyLayer;
        private Grid goldenSilhouette;
        private Rectangle goldenBody;
        private Canvas butterflyParticles;
        private readonly List<Path> morphButterflies = new List<Path>();
        private BitmapSource morphHuman;
        private BitmapSource morphFlight;
        private string effectsCharacter;
        private double effectsTime;

        private void InitializeEffects(Grid root)
        {
            goldenSilhouette = new Grid { Width = 330, Height = 330, VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false, Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(0.5, 0.82) };
            goldenBody = new Rectangle { Fill = new LinearGradientBrush(Color.FromRgb(255, 245, 190),
                Color.FromRgb(255, 161, 33), 90), Effect = new BlurEffect { Radius = 2 }, Opacity = 0.72 };
            goldenSilhouette.Children.Add(goldenBody);
            root.Children.Add(goldenSilhouette);
            butterflyLayer = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 42, 0, 0),
                IsHitTestVisible = false, Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(0.5, 0.82) };
            root.Children.Add(butterflyLayer);
            butterflyParticles = new Canvas { Width = 330, Height = 330, VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false, Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(0.5, 0.82) };
            Geometry wings = Geometry.Parse("M 0,0 C -9,-16 -16,-10 -8,-2 C -14,2 -10,10 0,1 C 10,10 14,2 8,-2 C 16,-10 9,-16 0,0 Z");
            wings.Freeze();
            for (int i = 0; i < 16; i++)
            {
                Path particle = new Path { Data = wings, Fill = new SolidColorBrush(Color.FromRgb(255, 218, 94)),
                    Stroke = Brushes.LemonChiffon, StrokeThickness = 0.7,
                    Effect = new DropShadowEffect { Color = Color.FromRgb(255, 166, 25), BlurRadius = 7, ShadowDepth = 0, Opacity = 0.9 } };
                morphButterflies.Add(particle);
                butterflyParticles.Children.Add(particle);
            }
            root.Children.Add(butterflyParticles);
            effectsCharacter = currentCharacter;
        }

        // Command handlers must not expose a raw butterfly for one frame before
        // OnTick has captured the human silhouette and started its dissolution.
        private void SetMovementSprite(BitmapSource source)
        {
            if (currentCharacter != "hutao") sprite.Source = PrepareNotebookFrame(source);
        }

        private void CancelButterflyEffects()
        {
            butterflyMorph.Reset();
            morphHuman = null;
            morphFlight = null;
            effectsCharacter = currentCharacter;
            sprite.Opacity = 1;
            sprite.OpacityMask = null;
            if (frames != null) sprite.Source = PrepareNotebookFrame(frames["idle"][0]);
            HideButterflyLayers();
        }

        private void HideButterflyLayers()
        {
            if (butterflyLayer == null) return;
            butterflyLayer.Visibility = Visibility.Collapsed;
            butterflyLayer.Source = null;
            goldenSilhouette.Visibility = Visibility.Collapsed;
            goldenBody.OpacityMask = null;
            goldenSilhouette.OpacityMask = null;
            butterflyParticles.Visibility = Visibility.Collapsed;
        }

        private static Brush DissolveMask(double progress, bool edge)
        {
            // A feathered gold edge travels from boots to hat. Smooth masks
            // keep the painted artwork soft; the emitted butterflies carry
            // the breakup, rather than exposing a square pixel grid.
            LinearGradientBrush mask = new LinearGradientBrush();
            mask.StartPoint = new Point(0, 0);
            mask.EndPoint = new Point(0, 1);
            double boundary = 1.15 - progress * 1.35;
            for (int step = 0; step <= 32; step++)
            {
                double y = step / 32.0;
                double distance = (y - boundary) / 0.14;
                double alpha = edge ? Math.Exp(-distance * distance * 2) : ButterflyMorphState.Ease((boundary - y) / 0.20 + 0.5);
                mask.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alpha * 255), 255, 255, 255), y));
            }
            mask.Freeze();
            return mask;
        }
        private void RenderButterflyEffects(BitmapSource requested, Transform pose, double seconds)
        {
            if (currentCharacter != effectsCharacter) CancelButterflyEffects();
            if (currentCharacter != "hutao" || movement == "held" || movement == "fall")
            {
                if (butterflyMorph.Flying || butterflyMorph.Progress > 0 || morphHuman != null)
                    CancelButterflyEffects();
                sprite.Source = requested;
                sprite.RenderTransform = pose;
                return;
            }
            bool flying = state == "idle" && movement == "walk" && activeGait == "run";
            if (flying)
            {
                BitmapSource[] humanRun = frames["run-human"];
                // Movement advances this phase by distance. Keep selecting its
                // stride throughout dissolution instead of freezing the pose
                // captured before the run began.
                morphHuman = humanRun[MotionCycle.Frame(runPhase, humanRun.Length, false)];
                morphFlight = requested;
            }
            else morphHuman = requested;
            butterflyMorph.Aim(flying);
            butterflyMorph.Advance(seconds);
            effectsTime += seconds;
            double p = butterflyMorph.Progress;
            sprite.RenderTransform = pose;
            if (p <= 0 || p >= 1)
            {
                sprite.Source = p <= 0 && flying ? morphHuman ?? frames["idle"][0] :
                    p >= 1 && !flying ? morphFlight ?? frames["walk"][0] : requested;
                sprite.Opacity = 1;
                sprite.OpacityMask = null;
                HideButterflyLayers();
                return;
            }

            sprite.Source = morphHuman ?? frames["idle"][0];
            sprite.Opacity = 1;
            sprite.OpacityMask = DissolveMask(p, false);
            goldenSilhouette.Visibility = Visibility.Visible;
            goldenBody.OpacityMask = new ImageBrush((ImageSource)sprite.Source) { Stretch = Stretch.Fill };
            goldenSilhouette.OpacityMask = DissolveMask(p, true);
            goldenSilhouette.RenderTransform = pose;

            double emerge = ButterflyMorphState.Ease((p - 0.22) / 0.68);
            butterflyLayer.Visibility = Visibility.Visible;
            butterflyLayer.Source = morphFlight ?? frames["walk"][0];
            butterflyLayer.Opacity = emerge;
            TransformGroup emergence = new TransformGroup();
            double scale = 0.25 + 0.75 * ButterflyMorphState.Ease(p);
            emergence.Children.Add(new ScaleTransform(scale, scale));
            emergence.Children.Add(new TranslateTransform(0, -25 * (1 - ButterflyMorphState.Ease(p))));
            emergence.Children.Add(pose);
            butterflyLayer.RenderTransform = emergence;

            butterflyParticles.Visibility = Visibility.Visible;
            butterflyParticles.RenderTransform = pose;
            for (int i = 0; i < morphButterflies.Count; i++)
            {
                Path particle = morphButterflies[i];
                double phase = (p - i * 0.023) / 0.63;
                double travel = ButterflyMorphState.Ease(phase);
                double startX = 72 + (i * 47 % 188);
                double startY = 295 - (i * 29 % 190);
                double x = startX + (190 - startX) * travel + Math.Sin(i * 2.1 + travel * 4) * 18 * travel;
                double y = startY + (150 - startY) * travel - Math.Sin(travel * Math.PI) * 34;
                double visible = phase > 0 && phase < 1 ? Math.Sin(phase * Math.PI) : 0;
                particle.Opacity = Math.Max(0, visible) * 0.94;
                TransformGroup particlePose = new TransformGroup();
                double flap = 0.35 + Math.Abs(Math.Sin(effectsTime * 20 + i));
                double size = 0.38 + (i % 3) * 0.12;
                particlePose.Children.Add(new ScaleTransform(size * flap, size));
                particlePose.Children.Add(new RotateTransform(-18 + i % 5 * 9));
                particlePose.Children.Add(new TranslateTransform(x, y));
                particle.RenderTransform = particlePose;
            }
        }
    }
}
