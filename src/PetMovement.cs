using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ClaudePet
{
    public sealed partial class PetWindow : Window
    {
        private List<Rect> monitorWorkAreas = new List<Rect>();
        private DateTime monitorsCachedAt = DateTime.MinValue;
        private Rect climbWorkArea;
        private double climbWallX;
        private Point jumpStart;
        private Point jumpEnd;
        private double jumpHeight;
        private double jumpDuration;
        private double jumpProgress;
        private PlatformEdge jumpTarget;
        private bool jumpIsAutomatic;
        private DateTime nextGanyuJump = DateTime.MinValue;
        private VaresaRunTrack varesaRunTrack;
        private double varesaRunAngle;
        private int varesaEdgeLaunchSide;

        private Point RunCenter { get { return new Point(CenterX, Top + Height - Width / 2); } }
        private bool IsVaresaSprint { get { return currentCharacter == "varesa" && activeGait == "run" && nightsoul.CanSprint; } }

        private void ClearVaresaRun()
        {
            varesaRunTrack = null;
            varesaRunAngle = 0;
            varesaEdgeLaunchSide = 0;
            sprite.RenderTransformOrigin = new Point(0.5, 0.82);
            sprite.RenderTransform = Transform.Identity;
        }

        private void EndVaresaRun()
        {
            ReleaseVaresaRun(false);
        }

        private void ReleaseVaresaRun(bool forceFall)
        {
            if (movement != "wallrun") { ClearVaresaRun(); return; }
            ClearVaresaRun();
            // Upright artwork must stay within the current monitor after
            // releasing a rotated wall pose, especially along the top edge.
            Rect work = GetWorkAreaForPoint(RunCenter);
            Left = Math.Max(work.Left, Math.Min(work.Right - Width, Left));
            double topInset = forceFall && HasGlider ? GlidingTopInset() : 0;
            Top = Math.Max(work.Top - topInset, Math.Min(work.Bottom - Height + FootInset, Top));
            PlatformEdge support = forceFall ? null : FindSupport();
            if (support != null) { supportHandle = support.Handle; StopWalking(); }
            else BeginFall(0);
        }

        private void StartVaresaRun(VaresaRunTrack track)
        {
            varesaRunTrack = track;
            movement = "wallrun";
            if (track.Window != null) supportHandle = track.Window.Handle;
            CancelWings();
            double angle;
            track.At(0, out angle);
            varesaRunAngle = angle;
        }

        private bool TryVaresaRun(PlatformEdge support, DateTime now, double seconds)
        {
            if (!IsVaresaSprint) return false;
            Rect work = GetWorkAreaForPoint(RunCenter);
            if (support.IsFloor)
            {
                double inset = VaresaRunTrack.ContactRadius(Width, Height);
                double radius = Math.Min(Width * 0.28, Math.Min(work.Width - 2 * inset, work.Height - 2 * inset) / 4);
                double margin = inset + Math.Max(0, radius);
                if (CenterX < work.Left + margin - 0.00001)
                {
                    varesaEdgeLaunchSide = -1;
                    direction = 1;
                    return false;
                }
                if (CenterX > work.Right - margin + 0.00001)
                {
                    varesaEdgeLaunchSide = 1;
                    direction = -1;
                    return false;
                }
                if (varesaEdgeLaunchSide != 0)
                {
                    direction = varesaEdgeLaunchSide;
                    varesaEdgeLaunchSide = 0;
                }
            }
            double travel = (moveSpeed + (targetMoveSpeed - moveSpeed) * Math.Min(1, seconds * 2.2)) * seconds;
            VaresaRunTrack approach = FindVaresaApproach(support.Handle, work, travel);
            if (approach != null) { StartVaresaRun(approach); UpdateVaresaRun(now, seconds); return true; }
            if (support.IsFloor)
            {
                VaresaRunTrack screen = VaresaRunTrack.Screen(work, RunCenter, Width, Height, direction);
                if (screen != null) { StartVaresaRun(screen); UpdateVaresaRun(now, seconds); return true; }
                // A legacy floor placement can be closer to an edge than the
                // rotated body fits. Walk inward first instead of snapping it.
                double margin = VaresaRunTrack.ContactRadius(Width, Height) + Width * 0.28;
                if (CenterX < work.Left + margin) direction = 1;
                else if (CenterX > work.Right - margin) direction = -1;
            }
            else
            {
                double edge = direction > 0 ? support.Right : support.Left;
                double gap = direction * (edge - CenterX) - Width * 0.38;
                if (gap <= travel)
                {
                    VaresaRunTrack wall = VaresaRunTrack.WindowSide(support, work, RunCenter, Width, Height, direction, false);
                    if (wall != null) { StartVaresaRun(wall); UpdateVaresaRun(now, seconds); return true; }
                }
            }
            return false;
        }

        private VaresaRunTrack FindVaresaApproach(IntPtr support, Rect work, double travel)
        {
            VaresaRunTrack nearest = null;
            double closest = Double.MaxValue;
            // A window whose side reaches the current running level can be
            // approached directly. Start the turning arc before its edge.
            foreach (PlatformEdge candidate in GetPlatforms(false))
            {
                if (candidate.IsFloor || candidate.Handle == support ||
                    candidate.Y >= RunCenter.Y - Width * 0.28 || candidate.Bottom < FootY - 3) continue;
                double side = direction > 0 ? candidate.Left : candidate.Right;
                double gap = direction * (side - RunCenter.X) - VaresaRunTrack.ContactRadius(Width, Height) - Width * 0.28;
                if (gap < -0.001 || gap > travel || gap > closest) continue;
                VaresaRunTrack approach = VaresaRunTrack.WindowSide(candidate, work, RunCenter, Width, Height, direction, true);
                if (approach != null) { nearest = approach; closest = gap; }
            }
            return nearest;
        }

        private void UpdateVaresaRun(DateTime now, double seconds)
        {
            VaresaRunTrack track = varesaRunTrack;
            if (!IsVaresaSprint || track == null || !GetMonitorWorkAreas().Contains(track.Work)) { EndVaresaRun(); return; }
            if (track.Window != null)
            {
                bool found = false;
                foreach (PlatformEdge window in GetPlatforms(false))
                    if (!window.IsFloor && window.Handle == track.Window.Handle &&
                        Math.Abs(window.Left - track.Window.Left) < 2 && Math.Abs(window.Right - track.Window.Right) < 2 &&
                        Math.Abs(window.Y - track.Window.Y) < 2 && Math.Abs(window.Bottom - track.Window.Bottom) < 2) { found = true; break; }
                bool vanished = ownHandle != IntPtr.Zero &&
                    (!NativeWindow.IsWindowVisible(track.Window.Handle) || NativeWindow.IsIconic(track.Window.Handle));
                if (!found || vanished) { ReleaseVaresaRun(true); return; }
            }
            if (now >= nextSpeedChange)
            {
                targetMoveSpeed = CurrentMovementSpeed(random.NextDouble());
                nextSpeedChange = now.AddMilliseconds(1200 + random.Next(1400));
            }
            moveSpeed += (targetMoveSpeed - moveSpeed) * Math.Min(1, seconds * 2.2);
            double distance = moveSpeed * Math.Max(0, seconds);
            if (track.Loop && Math.Abs(RunCenter.Y - (track.Work.Bottom - VaresaRunTrack.ContactRadius(Width, Height))) < 0.01 &&
                Math.Abs(Math.Sin(varesaRunAngle * Math.PI / 180)) < 0.00001)
            {
                VaresaRunTrack approach = FindVaresaApproach(IntPtr.Zero, track.Work, distance);
                if (approach != null) { StartVaresaRun(approach); track = approach; }
            }
            double remainingSeconds = 0;
            if (!track.Loop)
            {
                double available = Math.Max(0, track.Length - track.Distance);
                if (distance > available) remainingSeconds = (distance - available) / Math.Max(1, moveSpeed);
                distance = Math.Min(distance, available);
            }
            double requestedDistance = distance;
            distance = SpendNightsoul(distance, track.Work);
            remainingSeconds += (requestedDistance - distance) / Math.Max(1, moveSpeed);
            Point center = track.Advance(distance, out varesaRunAngle);
            Left = center.X - Width / 2;
            Top = center.Y - Height + Width / 2;
            runPhase = MotionCycle.Advance(runPhase, distance, Width * 0.42);
            if (!track.Loop && track.Distance >= track.Length - 0.00001)
            {
                bool landed = track.LandsOnWindow;
                ClearVaresaRun();
                if (landed)
                {
                    movement = "walk";
                    supportHandle = track.Window.Handle;
                    Top = track.Window.Y - Height + FootInset;
                }
                else BeginFall(direction * moveSpeed * 0.25);
                if (!nightsoul.CanSprint) ExhaustNightsoul();
                if (remainingSeconds > 0.000001) UpdateMovement(now, remainingSeconds);
            }
            else if (!nightsoul.CanSprint)
            {
                ExhaustNightsoul();
                if (remainingSeconds > 0.000001) UpdateMovement(now, remainingSeconds);
            }
            else if (track.Loop && now >= walkEnds && Math.Abs(center.Y - (track.Work.Bottom - VaresaRunTrack.ContactRadius(Width, Height))) < 0.01 &&
                Math.Abs(Math.Sin(varesaRunAngle * Math.PI / 180)) < 0.00001) StopWalking();
        }

        private List<Rect> GetMonitorWorkAreas()
        {
            if (monitorWorkAreas.Count > 0 && (DateTime.Now - monitorsCachedAt).TotalSeconds < 2) return monitorWorkAreas;
            List<Rect> result = new List<Rect>();
            NativeWindow.MonitorEnumProc callback = delegate(IntPtr handle, IntPtr dc, ref NativeWindow.RECT rectangle, IntPtr parameter) {
                NativeWindow.MONITORINFO info = new NativeWindow.MONITORINFO();
                info.Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeWindow.MONITORINFO));
                if (NativeWindow.GetMonitorInfo(handle, ref info)) result.Add(DesktopMovement.FromPixels(info.Work, fromDevice));
                return true;
            };
            NativeWindow.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            if (result.Count == 0) result.Add(SystemParameters.WorkArea);
            monitorWorkAreas = result;
            monitorsCachedAt = DateTime.Now;
            return result;
        }

        private Rect GetWorkAreaForPoint(Point dip)
        {
            return DesktopMovement.WorkAreaForPoint(dip, GetMonitorWorkAreas());
        }

        private Point CursorPosition(MouseEventArgs e)
        {
            NativeWindow.POINT cursor;
            if (NativeWindow.GetCursorPos(out cursor)) return fromDevice.Transform(new Point(cursor.X, cursor.Y));
            return fromDevice.Transform(PointToScreen(e.GetPosition(this)));
        }

        private void CancelAutomaticTraversal()
        {
            if (movement == "wallrun") EndVaresaRun();
            else if (movement == "walk") StopWalking();
            else if (movement == "climb" || movement == "jump")
            {
                PlatformEdge support = FindSupport();
                if (support != null) { supportHandle = support.Handle; StopWalking(); }
                else BeginFall(0);
            }
        }

        private void UpdateMovement(DateTime now, double deltaSeconds)
        {
            if (movement == "wallrun")
            {
                UpdateVaresaRun(now, deltaSeconds);
                return;
            }
            if (movement == "jump")
            {
                UpdateJump(now, deltaSeconds);
                return;
            }
            if (movement == "climb")
            {
                UpdateClimb(now, deltaSeconds);
                return;
            }
            if (movement == "fall")
            {
                UpdateFall(deltaSeconds);
                return;
            }
            if (ganyuApproaching)
            {
                UpdateGanyuApproach(now, deltaSeconds);
                return;
            }

            PlatformEdge platform = FindSupport();
            if (platform == null)
            {
                BeginFall(direction * Math.Max(24, moveSpeed * 0.45));
                return;
            }
            supportHandle = platform.Handle;
            Top = platform.Y - Height + FootInset;
            if (currentCharacter == "ganyu") platform = GanyuWalkingSupport(platform);

            if (currentCharacter == "ganyu" && autoWalk && !quietMode && now >= nextGanyuJump &&
                (movement == "walk" || now >= nextWalk) && TryStartGanyuJump(true)) return;

            if (movement == "walk")
            {
                if (TryVaresaRun(platform, now, deltaSeconds)) return;
                if (now >= nextSpeedChange)
                {
                    targetMoveSpeed = CurrentMovementSpeed(random.NextDouble());
                    nextSpeedChange = now.AddMilliseconds(1200 + random.Next(1400));
                }
                moveSpeed += (targetMoveSpeed - moveSpeed) * Math.Min(1, deltaSeconds * 2.2);
                double oldLeft = Left;
                double limit = direction > 0 ? platform.Right - Width * 0.68 : platform.Left - Width * 0.32;
                if (currentCharacter == "ganyu" && platform.IsFloor)
                    limit = direction > 0 ? platform.Right - GanyuTraversal.SideInset(Width) - Width / 2 :
                        platform.Left + GanyuTraversal.SideInset(Width) - Width / 2;
                if (IsVaresaSprint && platform.IsFloor && varesaEdgeLaunchSide != 0)
                {
                    Rect edgeWork = GetWorkAreaForPoint(RunCenter);
                    double inset = VaresaRunTrack.ContactRadius(Width, Height);
                    double radius = Math.Min(Width * 0.28, Math.Min(edgeWork.Width - 2 * inset, edgeWork.Height - 2 * inset) / 4);
                    double tangent = varesaEdgeLaunchSide < 0 ? edgeWork.Left + inset + Math.Max(0, radius) :
                        edgeWork.Right - inset - Math.Max(0, radius);
                    limit = direction > 0 ? Math.Min(limit, tangent - Width / 2) : Math.Max(limit, tangent - Width / 2);
                }
                bool sprinting = IsVaresaSprint;
                Rect sprintWork = sprinting ? GetWorkAreaForPoint(RunCenter) : Rect.Empty;
                double travel = moveSpeed * deltaSeconds;
                double sprintSpeed = moveSpeed, exhaustedRemainder = 0;
                if (sprinting) travel = Math.Min(travel, nightsoul.AvailableDistance(NightsoulCapacity(sprintWork)));
                double step = direction * travel;
                Left = direction > 0 ? Math.Min(limit, Left + step) : Math.Max(limit, Left + step);
                runPhase = MotionCycle.Advance(runPhase, Math.Abs(Left - oldLeft), Width * (activeGait == "stroll" ? 0.30 : 0.42));
                if (sprinting)
                {
                    SpendNightsoul(Math.Abs(Left - oldLeft), sprintWork);
                    if (!nightsoul.CanSprint)
                    {
                        exhaustedRemainder = Math.Max(0, deltaSeconds - Math.Abs(Left - oldLeft) / Math.Max(1, sprintSpeed));
                        ExhaustNightsoul();
                    }
                }
                double front = direction > 0 ? Left + Width * 0.68 : Left + Width * 0.32;
                bool reachedEdge = direction > 0 ? front >= platform.Right : front <= platform.Left;
                if (currentCharacter == "ganyu" && platform.IsFloor)
                    reachedEdge = Math.Abs(Left - limit) < 0.001;
                if (reachedEdge)
                {
                    if (IsVaresaSprint)
                    {
                        // Tight/occluded windows may not leave room for the
                        // rotated body. Never substitute the slow climb gait.
                        if (platform.IsFloor) direction = -direction;
                        else BeginFall(direction * moveSpeed * 0.25);
                    }
                    else if (platform.IsFloor)
                    {
                        StartClimbing(platform, direction, true);
                    }
                    else if (random.NextDouble() < 0.25)
                    {
                        BeginFall(direction * moveSpeed * 0.68);
                        ShowSpeech("哎呀，没抓稳！");
                    }
                    else
                    {
                        StartClimbing(platform, direction, false);
                    }
                }
                else if (now >= walkEnds)
                {
                    StopWalking();
                }
                if (exhaustedRemainder > 0.000001) UpdateMovement(now, exhaustedRemainder);
                return;
            }

            if (autoWalk && now >= nextWalk && platform.Right - platform.Left > Width * 1.25)
                BeginWalking(MovementGait.Choose(preferredGait, random.NextDouble()));
        }

        private void StartWalking()
        {
            BeginWalking("run");
        }

        private void StartStrolling()
        {
            BeginWalking("stroll");
        }

        private void StartGanyuJump()
        {
            if (currentCharacter != "ganyu") return;
            if (!TryStartGanyuJump(false)) ShowSpeech("附近暂时没有能安全蹬上去的窗台。");
        }

        private bool TryStartGanyuJump(bool automatic)
        {
            if (currentCharacter != "ganyu" || state != "idle" || pointerHeld ||
                (movement != "rest" && movement != "walk" && movement != "climb")) return false;
            if (automatic && (!autoWalk || quietMode)) return false;
            DateTime now = DateTime.Now;
            nextGanyuJump = now.AddSeconds(1.2);
            return StartGanyuTraversal(automatic);
        }

        private void UpdateJump(DateTime now, double deltaSeconds)
        {
            if (ganyuTraversal != null)
            {
                UpdateGanyuTraversal(now, deltaSeconds);
                return;
            }
            if (currentCharacter == "ganyu")
            {
                // Hide/drag/character changes can clear an in-flight route.
                // Its current wall endpoint is not the final roof endpoint.
                BeginFall(0);
                return;
            }
            if (jumpIsAutomatic && (!autoWalk || quietMode)) { BeginFall(0); return; }
            PlatformEdge currentTarget = null;
            foreach (PlatformEdge platform in GetPlatforms(false))
                if (DesktopMovement.SameLanding(jumpTarget, platform, jumpEnd.X)) { currentTarget = platform; break; }
            // Platform geometry uses a short cache; an
            // actual hidden/closed target must cancel immediately between refreshes.
            bool vanished = ownHandle != IntPtr.Zero &&
                (!NativeWindow.IsWindowVisible(jumpTarget.Handle) || NativeWindow.IsIconic(jumpTarget.Handle));
            if (currentTarget == null || vanished)
            {
                BeginFall(direction * 40 * Math.Max(0.6, Width / 165.0));
                return;
            }
            jumpProgress = Math.Min(1, jumpProgress + deltaSeconds / jumpDuration);
            Point foot = DesktopMovement.JumpPosition(jumpStart, jumpEnd, jumpProgress, jumpHeight);
            Left = foot.X - Width * 0.5;
            Top = foot.Y - Height + FootInset;
            if (jumpProgress >= 1)
            {
                supportHandle = currentTarget.Handle;
                nextGanyuJump = now.AddSeconds(8 + random.Next(7));
                StopWalking();
            }
        }

        private void ApplyMovementMode(string preference)
        {
            preferredGait = MovementGait.Normalize(preference);
            MovementGait.SavePreference(preferredGait);
            RefreshMovementChecks();
            if (ganyuApproaching) StopWalking();
            if (movement == "wallrun" && preferredGait != "run")
            {
                EndVaresaRun();
                SetActiveGait(MovementGait.Choose(preferredGait, random.NextDouble()), DateTime.Now);
            }
            if (state == "idle" && movement == "walk" && !pointerHeld)
                SetActiveGait(MovementGait.Choose(preferredGait, random.NextDouble()), DateTime.Now);
            else ScheduleNextWalk(DateTime.Now);
            ShowSpeech(currentCharacter == "deepseek" ? (preferredGait == "stroll" ? "慢慢游，想法会自己冒出来。" :
                preferredGait == "run" ? "摆摆尾巴，加速游！" : "快慢交替，自在游动。") : preferredGait == "stroll" ? "慢慢散步，也很舒服。" :
                preferredGait == "run" ? (currentCharacter == "hutao" ? "化作金蝶，飞过去啦。" : "接下来就奔跑吧！") :
                currentCharacter == "hutao" ? "散步、化蝶，自由活动。" : "走走跑跑，自由活动。" );
        }

        private void SetActiveGait(string gait, DateTime now)
        {
            activeGait = gait == "run" ? "run" : "stroll";
            varesaEdgeLaunchSide = 0;
            runPhase = 0;
            moveSpeed = CurrentMovementSpeed(random.NextDouble());
            targetMoveSpeed = moveSpeed;
            nextSpeedChange = now.AddMilliseconds(1200 + random.Next(1400));
            SetMovementSprite(frames[ActiveMovementFrameKey][0]);
        }

        private void BeginWalking(string gait)
        {
            if (movement == "wallrun")
            {
                if (gait == "run") { walkEnds = DateTime.Now.AddSeconds(8); return; }
                EndVaresaRun();
                SetActiveGait(gait, DateTime.Now);
            }
            if (state != "idle" || pointerHeld || (movement != "rest" && movement != "walk")) return;
            ClearGanyuTraversal();
            PlatformEdge platform = FindSupport();
            if (platform == null) return;
            supportHandle = platform.Handle;
            movement = "walk";
            movementStarted = DateTime.Now;
            SetActiveGait(gait, movementStarted);
            walkEnds = movementStarted.AddSeconds(quietMode ? 2 + random.NextDouble() * 2.5 : 5 + random.Next(7));
            direction = random.Next(2) == 0 ? -1 : 1;
            if (!quietMode)
                ShowSpeech(currentCharacter == "deepseek" ? (activeGait == "stroll" ? "我去慢慢游一圈。" : "摆摆尾巴，加速游！") : activeGait == "stroll" ? "我去慢慢散个步。" :
                    currentCharacter == "hutao" ? "化作金蝶，飞过去啦。" :
                    currentCharacter == "varesa" && nightsoul.CanSprint ? "戴好面具，全速前进！" : "我去小跑一会儿。");
        }

        private void StopWalking()
        {
            ClearGanyuTraversal();
            ClearVaresaRun();
            movement = "rest";
            SetMovementSprite(frames["idle"][0]);
            movementStarted = DateTime.Now;
            ScheduleNextWalk(DateTime.Now);
        }

        private void StartClimbing(PlatformEdge platform, int side, bool screenEdge)
        {
            if (currentCharacter == "ganyu")
            {
                if (TryStartGanyuJump(false)) return;
                // No useful opposing wall or safe roof: turn back on the
                // current ledge. Ganyu never enters the old climbing state.
                direction = -side;
                PlatformEdge support = FindSupport();
                if (support != null) StopWalking();
                else BeginFall(direction * 35 * Math.Max(0.6, Width / 165.0));
                return;
            }
            movement = "climb";
            movementStarted = DateTime.Now;
            climbPhase = 0;
            climbAttachLeft = Left;
            supportHandle = platform.Handle;
            climbSide = side;
            climbIsScreen = screenEdge;
            climbWorkArea = GetWorkAreaForPoint(new Point(CenterX, FootY - 1));
            climbWallX = side > 0 ? platform.Right : platform.Left;
            climbVerticalDirection = screenEdge ? -1 : 1;
            climbReverseAt = DateTime.Now.AddSeconds(3 + random.Next(3));
            ShowSpeech(screenEdge ? "沿着桌面边缘爬一会儿。" : "我抓住窗口边缘啦。");
        }

        private void AttachToWall(double wallX, DateTime now)
        {
            double target = MotionCycle.ClimbLeft(wallX, Width, MotionCycle.ClimbFacing(climbSide, climbIsScreen));
            double progress = Math.Max(0, Math.Min(1, (now - movementStarted).TotalSeconds / 0.22));
            double eased = progress * progress * (3 - 2 * progress);
            Left = climbAttachLeft + (target - climbAttachLeft) * eased;
        }

        private void UpdateClimb(DateTime now, double deltaSeconds)
        {
            // Also migrate a pending climb after a character switch or an
            // old in-memory state. Never spend even one tick sliding upward.
            if (currentCharacter == "ganyu")
            {
                if (!TryStartGanyuJump(false)) BeginFall(direction * 35 * Math.Max(0.6, Width / 165.0));
                return;
            }
            Rect work = climbIsScreen ? climbWorkArea : GetWorkAreaForPoint(new Point(CenterX, FootY - 1));
            double scale = Math.Max(0.6, Width / 165.0);
            double climbSpeed = DesktopMovement.ClimbSpeed(currentCharacter, Width);

            if (climbIsScreen)
            {
                AttachToWall(climbSide > 0 ? work.Right : work.Left, now);
                double upper = work.Top + Math.Max(8, Height * 0.12);
                double lower = work.Bottom - Height + FootInset;
                double oldTop = Top;
                Top = Math.Max(upper, Math.Min(lower, Top + climbVerticalDirection * climbSpeed * deltaSeconds));
                climbPhase = MotionCycle.Advance(climbPhase, oldTop - Top, Height * 0.16);
                if (climbVerticalDirection < 0 && (Top <= upper || now >= climbReverseAt))
                {
                    Top = Math.Max(upper, Top);
                    climbVerticalDirection = 1;
                    ShowSpeech("再爬回去。");
                }
                else if (climbVerticalDirection > 0 && Top >= lower)
                {
                    Top = lower;
                    supportHandle = IntPtr.Zero;
                    if (!autoWalk)
                    {
                        StopWalking();
                        return;
                    }
                    movement = "walk";
                    movementStarted = now;
                    SetActiveGait(MovementGait.Choose(preferredGait, random.NextDouble()), now);
                    walkEnds = now.AddSeconds(quietMode ? 2 + random.NextDouble() * 2 : 4 + random.Next(5));
                    direction = climbSide > 0 ? -1 : 1;
                }
                return;
            }

            PlatformEdge wall = null;
            double closestWall = Double.MaxValue;
            List<PlatformEdge> platforms = GetPlatforms(false);
            for (int i = 0; i < platforms.Count; i++)
            {
                if (platforms[i].Handle == supportHandle)
                {
                    double candidateX = climbSide > 0 ? platforms[i].Right : platforms[i].Left;
                    double distance = Math.Abs(candidateX - climbWallX);
                    if (distance < closestWall) { closestWall = distance; wall = platforms[i]; }
                }
            }
            if (wall == null || closestWall > Math.Max(48, Width * 0.7))
            {
                BeginFall(climbSide * 24 * scale);
                return;
            }

            climbWallX = climbSide > 0 ? wall.Right : wall.Left;
            AttachToWall(climbWallX, now);
            Top += climbVerticalDirection * climbSpeed * deltaSeconds;
            climbPhase = MotionCycle.Advance(climbPhase, -climbVerticalDirection * climbSpeed * deltaSeconds, Height * 0.16);
            double bottom = Math.Min(work.Bottom, wall.Bottom);
            if (climbVerticalDirection < 0 && FootY <= wall.Y)
            {
                Top = wall.Y - Height + FootInset;
                Left = Math.Max(wall.Left - Width * 0.25, Math.Min(wall.Right - Width * 0.75, Left));
                supportHandle = wall.Handle;
                StopWalking();
                return;
            }
            if (climbVerticalDirection > 0 && Top + Height * 0.62 >= bottom)
            {
                BeginFall(climbSide * 28 * scale);
                ShowSpeech("到下面啦！");
            }
        }

        private void BeginFall(double horizontalSpeed)
        {
            if (movement == "fall") return;
            // A cancelled leap may still be above its launch window. It must
            // be allowed to land there; walking/climbing off an edge still
            // excludes that window so the pet can descend past its side.
            fallFromHandle = movement == "jump" || movement == "wallrun" ? IntPtr.Zero : supportHandle;
            ClearGanyuTraversal();
            ClearVaresaRun();
            movement = "fall";
            movementStarted = DateTime.Now;
            fallVelocityX = horizontalSpeed;
            fallVelocityY = 16 * Math.Max(0.6, Width / 165.0);
            landedFromFall = false;
        }

        private void UpdateFall(double deltaSeconds)
        {
            double oldFoot = FootY;
            double descent;
            if (HasGlider || currentCharacter == "gpt6a" || currentCharacter == "deepseek")
                descent = GlidePhysics.Step(ref fallVelocityX, ref fallVelocityY,
                    HasGlider ? glideAnimation.Openness : 1, Width, deltaSeconds);
            else
            {
                descent = fallVelocityY * deltaSeconds;
                fallVelocityY = Math.Min(520, fallVelocityY + 420 * deltaSeconds);
            }
            Left += fallVelocityX * deltaSeconds;
            Rect work = GetWorkAreaForPoint(new Point(CenterX, FootY));
            if (CenterX < work.Left + Width * 0.18)
            {
                Left = work.Left - Width * 0.32;
                fallVelocityX = Math.Abs(fallVelocityX);
            }
            else if (CenterX > work.Right - Width * 0.18)
            {
                Left = work.Right - Width * 0.68;
                fallVelocityX = -Math.Abs(fallVelocityX);
            }
            Top += descent;
            double newFoot = FootY;
            double center = CenterX;
            PlatformEdge landing = null;

            List<PlatformEdge> platforms = GetPlatforms(false);
            for (int i = 0; i < platforms.Count; i++)
            {
                PlatformEdge candidate = platforms[i];
                if (candidate.Handle == fallFromHandle && fallFromHandle != IntPtr.Zero) continue;
                if (center < candidate.Left + 4 || center > candidate.Right - 4) continue;
                if (oldFoot <= candidate.Y + 2 && newFoot >= candidate.Y - 2)
                {
                    if (landing == null || candidate.Y < landing.Y) landing = candidate;
                }
            }

            // Dropping onto a lower-resolution monitor can put the feet below
            // its taskbar already. Recover on that monitor, never the primary.
            if (landing == null && newFoot >= work.Bottom)
                landing = new PlatformEdge { Handle = IntPtr.Zero, Left = work.Left, Right = work.Right,
                    Y = work.Bottom, Bottom = work.Bottom, IsFloor = true };

            if (landing != null)
            {
                Top = landing.Y - Height + FootInset;
                supportHandle = landing.Handle;
                movement = "rest";
                movementStarted = DateTime.Now;
                ScheduleNextWalk(DateTime.Now);
                if (!landedFromFall)
                {
                    landedFromFall = true;
                    ShowSpeech(landing.IsFloor ? "安全落地。" : "还好接住了我。");
                }
            }
        }

        private PlatformEdge FindSupport()
        {
            List<PlatformEdge> platforms = GetPlatforms(false);
            PlatformEdge byHandle = null;
            for (int i = 0; i < platforms.Count; i++)
            {
                PlatformEdge candidate = platforms[i];
                if (candidate.Handle == supportHandle && CenterX >= candidate.Left && CenterX <= candidate.Right &&
                    (byHandle == null || Math.Abs(FootY - candidate.Y) < Math.Abs(FootY - byHandle.Y)))
                {
                    byHandle = candidate;
                }
            }

            if (byHandle != null && CenterX >= byHandle.Left && CenterX <= byHandle.Right && Math.Abs(FootY - byHandle.Y) < Math.Max(22, Height * 0.3))
                return byHandle;

            PlatformEdge nearest = null;
            double best = double.MaxValue;
            for (int i = 0; i < platforms.Count; i++)
            {
                PlatformEdge candidate = platforms[i];
                if (CenterX < candidate.Left || CenterX > candidate.Right) continue;
                double distance = Math.Abs(FootY - candidate.Y);
                if (distance < Math.Max(12, Height * 0.08) && distance < best)
                {
                    best = distance;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        private List<PlatformEdge> GetPlatforms(bool force)
        {
            if (!force && (DateTime.Now - platformCacheTime).TotalMilliseconds < 400)
                return platformCache;

            List<Rect> workAreas = GetMonitorWorkAreas();
            List<PlatformEdge> result = new List<PlatformEdge>();
            foreach (Rect work in workAreas)
                result.Add(new PlatformEdge { Handle = IntPtr.Zero, Left = work.Left, Right = work.Right,
                    Y = work.Bottom, Bottom = work.Bottom, IsFloor = true });
            List<Rect> occluders = new List<Rect>();
            List<GanyuSceneWindow> ganyuScene = new List<GanyuSceneWindow>();

            uint ownProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            NativeWindow.EnumWindowsProc callback = delegate(IntPtr handle, IntPtr parameter) {
                uint windowProcessId;
                NativeWindow.GetWindowThreadProcessId(handle, out windowProcessId);
                if (windowProcessId == ownProcessId || handle == ownHandle || !NativeWindow.IsWindowVisible(handle) || NativeWindow.IsIconic(handle)) return true;
                int cloaked;
                try { if (NativeWindow.DwmGetWindowAttribute(handle, 14, out cloaked, 4) == 0 && cloaked != 0) return true; }
                catch (DllNotFoundException) { }
                NativeWindow.RECT pixelRect;
                if (!NativeWindow.GetWindowRect(handle, out pixelRect)) return true;
                Rect bounds = DesktopMovement.FromPixels(pixelRect, fromDevice);
                if (bounds.Width < 80 || bounds.Height < 40) return true;
                bool isTool = (NativeWindow.GetWindowLong(handle, -20) & 0x00000080) != 0;
                System.Text.StringBuilder className = new System.Text.StringBuilder(128);
                AbilityNative.GetClassName(handle, className, className.Capacity);
                string windowClass = className.ToString();
                bool shell = windowClass == "Progman" || windowClass == "WorkerW" ||
                    windowClass == "Shell_TrayWnd" || windowClass == "Shell_SecondaryTrayWnd";
                bool clickThrough = (NativeWindow.GetWindowLong(handle, -20) & 0x00000020) != 0;
                bool backdrop = IsGanyuBackdrop(bounds, workAreas);
                if (!shell && !clickThrough)
                    ganyuScene.Add(new GanyuSceneWindow { Handle = handle, Bounds = bounds, IsBackdrop = backdrop,
                        CanBounce = !backdrop && !isTool && NativeWindow.GetWindowTextLength(handle) > 0 && bounds.Width >= 150 && bounds.Height >= 90 });
                if (!isTool && bounds.Width >= 150 && bounds.Height >= 90 && NativeWindow.GetWindowTextLength(handle) > 0)
                {
                    foreach (Rect work in workAreas)
                    {
                        if (bounds.Top < work.Top + Math.Max(45, Height * 0.42) || bounds.Top > work.Bottom - 45 ||
                            bounds.Right < work.Left || bounds.Left > work.Right) continue;
                        PlatformEdge edge = new PlatformEdge { Handle = handle, Left = Math.Max(work.Left, bounds.Left),
                            Right = Math.Min(work.Right, bounds.Right), Y = bounds.Top, Bottom = bounds.Bottom };
                        foreach (PlatformEdge visible in DesktopMovement.VisibleTopSegments(edge, occluders,
                            currentCharacter == "ganyu" ? Width * 0.79 : Height * 0.88))
                            if (visible.Right - visible.Left >= Width * 0.4) result.Add(visible);
                    }
                }
                // EnumWindows arrives front to back. Even a dialog that cannot
                // be stood on can obscure the top of a window behind it.
                occluders.Add(bounds);
                return true;
            };
            NativeWindow.EnumWindows(callback, IntPtr.Zero);
            platformCache = result;
            platformCacheTime = DateTime.Now;
            ganyuSceneWindows = ganyuScene;
            ganyuSceneTimestamp = platformCacheTime;
            return result;
        }

        private void PlaceAtStart()
        {
            List<PlatformEdge> platforms = GetPlatforms(true);
            PlatformEdge target = null;
            for (int i = 0; i < platforms.Count; i++)
            {
                if (platforms[i].Handle == startupForeground)
                {
                    target = platforms[i];
                    break;
                }
            }
            NativeWindow.POINT cursor;
            Point position = NativeWindow.GetCursorPos(out cursor) ? fromDevice.Transform(new Point(cursor.X, cursor.Y)) : new Point(0, 0);
            Rect work = GetWorkAreaForPoint(position);
            if (target == null)
                foreach (PlatformEdge platform in platforms)
                    if (platform.IsFloor && platform.Left == work.Left && platform.Y == work.Bottom) { target = platform; break; }
            if (target == null) target = platforms[0];
            supportHandle = target.Handle;
            Left = Math.Max(target.Left, target.Right - Width - 24);
            Top = target.Y - Height + FootInset;
        }

        private void OnPointerDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            tapSequence.Begin(currentCharacter, e.ClickCount);
            CancelWindowAbility();
            ClearGanyuTraversal();
            bool airborne = movement == "fall" || movement == "held" || movement == "jump" || movement == "wallrun";
            ClearVaresaRun();
            Focus();
            pointerHeld = true;
            // Catching an airborne character is already a hold, even before
            // the mouse crosses the ground-click drag threshold. Keep the
            // glider open and let releasing the catch resume the fall.
            dragged = airborne;
            movement = airborne ? "held" : "rest";
            CancelButterflyEffects();
            state = "idle";
            stateEnds = DateTime.MaxValue;
            Point screenPoint = CursorPosition(e);
            pointerStart = screenPoint;
            lastDragPoint = screenPoint;
            lastDragTime = DateTime.Now;
            dragVelocityX = 0;
            dragVelocityY = 0;
            heldTilt = 0;
            windowStart = new Point(Left, Top);
            CaptureMouse();
            e.Handled = true;
        }

        private void OnPointerMove(object sender, MouseEventArgs e)
        {
            if (!pointerHeld || e.LeftButton != MouseButtonState.Pressed) return;
            Point current = CursorPosition(e);
            Vector delta = current - pointerStart;
            tapSequence.Move(Math.Abs(delta.X) + Math.Abs(delta.Y));
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) > 5) dragged = true;
            if (dragged)
            {
                movement = "held";
                DateTime now = DateTime.Now;
                Vector step = current - lastDragPoint;
                double dragDelta = Math.Max(0.004, (now - lastDragTime).TotalSeconds);
                if (dragDelta <= 0.16)
                {
                    dragVelocityX = Math.Max(-280, Math.Min(280, step.X / dragDelta));
                    dragVelocityY = Math.Max(-220, Math.Min(300, step.Y / dragDelta));
                }
                heldTilt = Math.Max(-13, Math.Min(13, dragVelocityX * 0.045));
                Point location = DesktopMovement.DragLocation(current, pointerStart, windowStart);
                Left = location.X;
                Top = location.Y;
                lastDragPoint = current;
                lastDragTime = now;
            }
        }

        private void OnPointerUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || !pointerHeld) return;
            Vector releaseDelta = CursorPosition(e) - pointerStart;
            tapSequence.Move(Math.Abs(releaseDelta.X) + Math.Abs(releaseDelta.Y));
            bool abilityDoubleClick = tapSequence.Complete(currentCharacter);
            pointerHeld = false;
            ReleaseMouseCapture();
            if (!dragged)
            {
                movement = "rest";
                PlatformEdge platform = FindClosestPlatform(Height * 0.16);
                if (platform != null)
                {
                    supportHandle = platform.Handle;
                    Top = platform.Y - Height + FootInset;
                }
                if (!abilityDoubleClick) Play("happy", phrases[random.Next(phrases.Length)], 1500, 170);
            }
            else
            {
                if ((DateTime.Now - lastDragTime).TotalMilliseconds > 160)
                {
                    dragVelocityX = 0;
                    dragVelocityY = 16;
                }
                supportHandle = IntPtr.Zero;
                movement = "rest";
                direction = dragVelocityX < 0 ? -1 : 1;
                BeginFall(dragVelocityX);
                fallVelocityY = Math.Max(-220, Math.Min(300, dragVelocityY));
                heldTilt = 0;
                if (!abilityDoubleClick) ShowSpeech("放手啦！");
            }
            if (abilityDoubleClick && !HandleAiDoubleClick()) ReduceWindowAbilityCooldownFromDoubleClick();
        }

        private PlatformEdge FindClosestPlatform(double range)
        {
            PlatformEdge nearest = null;
            double best = double.MaxValue;
            List<PlatformEdge> platforms = GetPlatforms(true);
            for (int i = 0; i < platforms.Count; i++)
            {
                PlatformEdge candidate = platforms[i];
                if (CenterX < candidate.Left || CenterX > candidate.Right) continue;
                double distance = Math.Abs(FootY - candidate.Y);
                if (distance <= range && distance < best)
                {
                    nearest = candidate;
                    best = distance;
                }
            }
            return nearest;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ResizePet(e.Delta > 0 ? 1.08 : 0.92, false);
            e.Handled = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                return;
            }
            if (e.Key == Key.Add || e.Key == Key.OemPlus) ResizePet(1.08, false);
            else if (e.Key == Key.Subtract || e.Key == Key.OemMinus) ResizePet(0.92, false);
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0) ResizePet(1, true);
            else return;
            e.Handled = true;
        }

        private void ResizePet(double factor, bool reset)
        {
            if (ganyuApproaching) StopWalking();
            bool leftWall = movement == "wallrun";
            if (leftWall) EndVaresaRun();
            double oldWidth = Width;
            double oldHeight = Height;
            if (reset)
            {
                Width = 165;
                Height = 225;
            }
            else
            {
                Width = Math.Max(MinWidth, Math.Min(MaxWidth, Width * factor));
                Height = Math.Max(MinHeight, Math.Min(MaxHeight, Height * factor));
            }
            Left -= (Width - oldWidth) / 2;
            Top -= Height - oldHeight;
            double speedScale = Math.Max(0.6, Width / 165.0) / Math.Max(0.6, oldWidth / 165.0);
            moveSpeed *= speedScale;
            targetMoveSpeed *= speedScale;
            platformCacheTime = DateTime.MinValue;
            // Keep the feet in place while resizing, including on monitors with
            // negative desktop origins and independent taskbar work areas.
            Top += FootInset - oldHeight * 0.038;
            if (leftWall)
            {
                Rect work = GetWorkAreaForPoint(RunCenter);
                Left = Math.Max(work.Left, Math.Min(work.Right - Width, Left));
                Top = Math.Max(work.Top, Math.Min(work.Bottom - Height + FootInset, Top));
            }
            if (movement == "jump") BeginFall(0);
        }

    }
}
