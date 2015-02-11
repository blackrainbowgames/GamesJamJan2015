﻿using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common;
using Assets.Scripts.Views;
using UnityEngine;

namespace Assets.Scripts.Logic
{
    public partial class Engine : Script
    {
        public void Awake()
        {
            AdBuddizBinding.SetLogLevel(AdBuddizBinding.ABLogLevel.Info);
            AdBuddizBinding.SetAndroidPublisherKey("bdc2f780-6d67-4ad9-9545-5092d50bf19a");
            AdBuddizBinding.CacheAds();
        }

        public void Start()
        {
            DetectLanguage();
            GetComponent<Menu>().Open();

            //var level = GameData.HardLevels[3];
            //level.Generator = true;
            //Level = level;
            //Level.Progress = 0;
            //StartGame();
        }

        public void Update()
        {
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                GoBack();
            }
        }

        public static int CalcScore(List<List<Person>> tables)
        {
            var score = 0;

            foreach (var table in tables)
            {
                score += Table.GetSympathy(table[0], table[1]);
            }

            return score;
        }

        public static int CalcScore()
        {
            var tables = FindObjectsOfType<Table>().Select(i => i.GetComponentsInChildren<Character>().Select(j => j.Person).ToList()).ToList();

            return CalcScore(tables);
        }

        public static bool CanShift
        {
            get
            {
                return Level.Type == LevelType.Swap ? Level.Swaps - Swaps > 0 : (Timeout - DateTime.Now).TotalSeconds > 0;
            }
        }

        private void BeginGame()
        {
            foreach (var table in FindObjectsOfType<Table>())
            {
                Destroy(table.gameObject);
            }

            GameData.EasyLevels.Shuffle();

            var tables = Level.Generator ? GenerateTables(Level.TableNumber, Level.Target, Level.Сomplexity) : InitializeTables(Level);

            Timeout = DateTime.Now.AddSeconds(Level.Time);
            Swaps = 0;

            RemoveUnusedHobbies(tables);

            for (var i = 0; i < Level.TableNumber; i++)
            {
                var table = PrefabsHelper.Instantiate(Level.TableName, Get<Play>().GameTransform);
                var characters = table.GetComponentsInChildren<Character>();

                Get<Play>().Background.mainTexture = Resources.Load<Texture2D>("Images/Background/" + Level.Background);

                characters[0].Initialize(tables[i][0]);
                characters[1].Initialize(tables[i][1]);

                table.transform.localPosition = GameData.TablePositions[Level.TableNumber][i];
                table.transform.localScale = GameData.TableScales[Level.TableNumber] * Vector3.one;
            }

            State = GameState.Playing;
        }

        private static void RemoveUnusedHobbies(List<List<Person>> tables)
        {
            var hobbies = new List<Hobby>();

            foreach (var table in tables)
            {
                hobbies.AddRange(table[0].Hobbies);
                hobbies.AddRange(table[1].Hobbies);
            }

            foreach (var table in tables)
            {
                table[0].Hobbies.RemoveAll(i => hobbies.Count(j => j == i) == 1);
                table[1].Hobbies.RemoveAll(i => hobbies.Count(j => j == i) == 1);
            }
        }

        private static List<List<Person>> InitializeTables(Level level)
        {
            GameData.Shuffle();

            var tables = new List<List<Person>>();
            var boys = new List<Person>();
            var girls = new List<Person>();
            var hobbyShift = CRandom.GetRandom(0, 10);

            for (var i = 0; i < level.TableNumber; i++)
            {
                for (var j = 0; j < level.MaleHobbies[i].Count; j++)
                {
                    level.MaleHobbies[i][j] = (Hobby) IncMod((int) level.MaleHobbies[i][j], hobbyShift, GameData.Hobbies.Count);
                }

                for (var j = 0; j < level.FemaleHobbies[i].Count; j++)
                {
                    level.FemaleHobbies[i][j] = (Hobby) IncMod((int) level.FemaleHobbies[i][j], hobbyShift, GameData.Hobbies.Count);
                }

                tables.Add(new List<Person>());

                var maleImage = GameData.GetNextMaleImage();
                var femaleImage = GameData.GeNextFemaleImage();

                boys.Add(new Person
                {
                    Name = GameData.GetNameByImage(maleImage, level.JapanNames),
                    Image = maleImage,
                    Male = true,
                    Hobbies = level.MaleHobbies[i]
                });
                girls.Add(new Person
                {
                    Name = GameData.GetNameByImage(femaleImage, level.JapanNames),
                    Image = femaleImage,
                    Male = false,
                    Hobbies = level.FemaleHobbies[i]
                });
            }

            if (level.Formation != null)
            {
                for (var i = 0; i < level.Formation.Count; i++)
                {
                    tables[i] = new List<Person>
                    {
                        boys[level.Formation[i][0]],
                        girls[level.Formation[i][1]]
                    };
                }

                return Shuffle(tables);
            }

            for (var i = 0; i < level.TableNumber; i++)
            {
                tables[i] = new List<Person> { boys[i], girls[i] };
            }

            List<List<Person>> worst, best;
            int max, complexity;

            Analize(tables, out worst, out best, out max, out complexity);

            if (max != Level.Target)
            {
                throw new Exception(Convert.ToString(max));
            }

            return Shuffle(worst);
        }

        private static List<List<Person>> Shuffle(List<List<Person>> tables)
        {
            foreach (var table in tables)
            {
                if (CRandom.Chance(0.5f))
                {
                    table.Reverse();
                }
            }

            if (Settings.Debug)
            {
                var formation = new List<string>();

                foreach (var table in tables)
                {
                    var i = table[0].Male ? Level.MaleHobbies.IndexOf(table[0].Hobbies) : Level.FemaleHobbies.IndexOf(table[0].Hobbies);
                    var j = table[1].Male ? Level.MaleHobbies.IndexOf(table[1].Hobbies) : Level.FemaleHobbies.IndexOf(table[1].Hobbies);

                    formation.Add(table[0].Male
                        ? string.Format("new List<int> {{ {0}, {1} }}", i, j)
                        : string.Format("new List<int> {{ {0}, {1} }}", j, i));
                }
            
                Debug.Log("Formation = new List<List<int>> {" + string.Join(", ", formation.ToArray()) + " }");
            }

            return tables.Shuffle();
        }

        private static int IncMod(int source, int value, int mod)
        {
            if (source >= mod) throw new ArgumentException();

            var result = source + value;

            if (result >= mod)
            {
                result -= mod;
            }

            return result;
        }

        private static void DetectLanguage()
        {
            var languages = (TextAsset) Resources.Load("Localization", typeof(TextAsset));

            Localization.LoadCSV(languages);

            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                case SystemLanguage.Ukrainian:
                case SystemLanguage.Belarusian:
                    Localization.language = "Russian";
                    break;
                default:
                    Localization.language = "English";
                    break;
            }
        }
    }
}