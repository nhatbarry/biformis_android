using System.Collections.Generic;
using UnityEngine;

public static partial class BossPhaseTwoCombatSetup
{
    // Object-space blade segments, matching each original Aseprite canvas/pivot.
    private static readonly Dictionary<string, Vector4[]> BladePoses = new()
    {
        ["horizontalSlash"] = new[]
        {
            new Vector4(1.13532f, 1.02177f, 1.56855f, 1.84696f),
            new Vector4(0.55769f, 0.56792f, 0.39266f, 0.98051f),
            new Vector4(-0.12308f, 1.10429f, -0.78323f, 1.64066f),
            new Vector4(1.32099f, 1.00114f, 1.98114f, 1.43437f),
            new Vector4(1.42414f, 0.98051f, 2.29058f, 1.14555f),
            new Vector4(0.90840f, 0.46477f, 1.44477f, 0.07281f),
            new Vector4(0.94966f, 0.46477f, 1.40351f, 0.15533f),
            new Vector4(1.25910f, 1.02177f, 1.56855f, 1.90885f),
            new Vector4(0.97029f, 1.04240f, 1.52729f, 1.86759f),
        },
        ["rangedCharge"] = new[]
        {
            new Vector4(0.70987f, 0.54210f, 1.27790f, 0.12899f),
            new Vector4(0.65823f, 0.54210f, 1.26069f, 0.09456f),
            new Vector4(0.72709f, 0.54210f, 1.27790f, 0.11178f),
            new Vector4(0.67545f, 0.54210f, 1.26069f, 0.09456f),
            new Vector4(0.76151f, 0.54210f, 1.27790f, 0.11178f),
            new Vector4(0.70987f, 0.54210f, 1.12299f, 0.09456f),
            new Vector4(0.69266f, 0.54210f, 1.26069f, 0.11178f),
            new Vector4(0.70987f, 0.54210f, 1.24348f, 0.11178f),
        },
        ["dashStab"] = new[]
        {
            new Vector4(0.77614f, 0.58778f, 1.50785f, 0.09280f),
            new Vector4(-0.66576f, 0.56626f, -1.05314f, 0.22192f),
            new Vector4(0.96983f, 0.37257f, 1.63698f, 0.30801f),
            new Vector4(1.07743f, 0.71690f, 1.57242f, 1.01820f),
            new Vector4(0.73310f, 0.88907f, 1.48633f, 0.95363f),
            new Vector4(0.71158f, 0.84603f, 1.42177f, 1.01820f),
            new Vector4(0.71158f, 0.43713f, 1.48633f, 0.13584f),
            new Vector4(0.73310f, 0.65234f, 1.42177f, 0.11432f),
            new Vector4(0.77614f, 0.63082f, 1.48633f, 0.11432f),
        },
        ["verticalSlash"] = new[]
        {
            new Vector4(0.74548f, 0.58397f, 1.41870f, 0.05004f),
            new Vector4(1.09370f, 1.65183f, 0.95441f, 2.48755f),
            new Vector4(1.09370f, 1.69826f, 1.00084f, 2.41791f),
            new Vector4(1.14012f, 1.79112f, 1.02405f, 2.83577f),
            new Vector4(1.14012f, 1.79112f, 1.02405f, 2.78934f),
            new Vector4(0.72227f, 0.56076f, 1.11691f, -0.04282f),
            new Vector4(0.67584f, 0.56076f, 1.58120f, 0.05004f),
            new Vector4(0.76869f, 0.56076f, 1.55798f, 0.02683f),
            new Vector4(0.79191f, 0.56076f, 1.58120f, 0.00361f),
        },
    };
}
