﻿using System;
using Assets.Scripts.Common;
using Assets.Scripts.Views;
using UnityEngine;

namespace Assets.Scripts.Logic
{
    public partial class Engine
    {
        public static Level Level;
        public static int TaskId = 999;
        public static DateTime Timeout;
        public static TimeSpan Timeleft;
        public static int Swaps;

        private static GameState _state;

        public static GameState State
        {
            get { return _state; }
            set
            {
                _state = value;
                
                Time.timeScale = value == GameState.Paused ? 0 : 1;
                
                if (Time.timeScale > 0)
                {
                    Find<AudioPlayer>().ScheduleFix();
                }
            }
        }

        public void StartGame()
        {
            TaskScheduler.Kill(TaskId++);

            State = GameState.Ready;

            var play = Get<Play>();

            play.Open(BeginGame);

            if ((Level.Type == LevelType.Easy || Level.Type == LevelType.Swap) && Level.Progress == 0)
            {
                TaskScheduler.CreateTask(() => PauseGame(play.HelpDialog), TaskId, 1);
            }
        }

        public void PlayNext()
        {
            var levels = GameData.GetLevels(Level.Type);

            if (Level.Progress < levels.Count - 1)
            {
                var progress = Level.Progress + 1;

                Level = levels[progress];
                Level.Progress = progress;

                StartGame();
            }
            else
            {
                ExitGame();
            }
        }

        public void PauseGame(TweenPanel dialog)
        {
            Timeleft = Timeout - DateTime.Now;
            Get<Play>().ShowDialog(dialog);
            State = GameState.Paused;
        }

        public void ResumeGame()
        {
            Timeout = DateTime.Now.Add(Timeleft);
            GetComponent<Play>().CloseDialog();
            State = GameState.Playing;
        }

        public void CompleteGame()
        {
            State = GameState.Paused;

            var score = CalcScore();

            if (Level.Type == LevelType.Easy)
            {
                if (score >= Level.Target && Profile.ProgressEasy == Level.Progress)
                {
                    Profile.ProgressEasy++;
                }
            }
            else if (Level.Type == LevelType.Hard)
            {
                if (score >= Level.Target && Profile.ProgressHard == Level.Progress)
                {
                    Profile.ProgressHard++;
                }
            }
            else if (Level.Type == LevelType.Swap)
            {
                if (score >= Level.Target && Profile.ProgressSwap == Level.Progress)
                {
                    Profile.ProgressSwap++;
                }
            }

            var play = Get<Play>();

            play.SetScoreDialog(score >= Level.Target);
            play.ShowDialog(play.ScoreDialog);

            if (DateTime.UtcNow > Profile.ShowAdTime.AddMinutes(5) && AdBuddizBinding.IsReadyToShowAd())
            {
                Profile.ShowAdTime = DateTime.UtcNow;
                TaskScheduler.CreateTask(AdBuddizBinding.ShowAd, 1f);
            }
        }

        public void ExitGame()
        {
            if (State != GameState.Paused) return;

            TaskScheduler.Kill(TaskId);

            switch (Level.Type)
            {
                case LevelType.Easy:
                    Get<EasyLevels>().Open();
                    break;
                case LevelType.Hard:
                    Get<HardLevels>().Open();
                    break;
                case LevelType.Swap:
                    Get<SwapLevels>().Open();
                    break;
            }

            State = GameState.Ready;
        }
    }
}