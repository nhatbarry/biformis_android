using System;

namespace CaptainPinkTurd.DataPersistence.Data
{
    public class GameData
    {
        public bool hasDoneTutorial = false;
        public int highScore = 0;
        public int storyStep = -1; //index into StoryData.steps, -1 = no story in progress
        public bool storyCompleted = false;

        public int GetPercentageComplete()
        {
            throw new NotImplementedException();
        }
    }
}