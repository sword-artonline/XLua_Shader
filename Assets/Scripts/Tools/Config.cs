using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Config
{
    private static string ABPath_Pre = Application.dataPath.Substring(0, Application.dataPath.Length - 6);

    public static string ABPath = ABPath_Pre + "ABResources";
}
