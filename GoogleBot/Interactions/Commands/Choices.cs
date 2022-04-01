using System.Collections.Generic;
using System.Linq;
using GoogleBot.Interactions.CustomAttributes;

namespace GoogleBot.Interactions.Commands;

/// <summary>
/// A class for choices for <see cref="ChoicesAttribute"/>
/// Choices have to be declared in the dictionary and a const int value 
/// </summary>
public class Choices
{
    private static readonly Dictionary<int, (string, int)[]> AllChoices = new Dictionary<int, (string, int)[]>
    {
        {choicesTest, new []{ ("Yes", 1), ("No", 2), ("Maybe", 3)}},
        {choicesTest2, new []{("Option1", 1), ("Option2", 22), ("Option3", 3)}}
    };
    
        
    public const int choicesTest = 0;
    public const int choicesTest2 = 1;

    public static (string, int)[] Get(int id)
    {
        return AllChoices[id];
    }

    public static Dictionary<int, string> GetChoices(int id)
    {
        (string, int)[] choices = AllChoices[id];
        return choices.ToDictionary(valueTuple => valueTuple.Item2, valueTuple => valueTuple.Item1);
    }
}