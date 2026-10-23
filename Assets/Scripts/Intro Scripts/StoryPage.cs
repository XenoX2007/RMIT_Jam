using UnityEngine;

[System.Serializable]
public class StoryPage
{
    [TextArea(5, 10)]
    public string storyText;

    public Sprite image;
}