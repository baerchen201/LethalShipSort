using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LethalShipSort;

public struct ItemPosition
{
    public ItemPosition(string s)
    {
        Match match = new Regex(
            @"(?:([\w/\\]+):)?(?:([\d-.]+),){2}([\d-.]+)$",
            RegexOptions.Multiline
        ).Match(s);
        if (!match.Success)
            throw new ArgumentException("Invalid format");

        if (
            !float.TryParse(match.Groups[2].Captures[0].Value, out var x)
            || !float.TryParse(match.Groups[2].Captures[1].Value, out var y)
            || !float.TryParse(match.Groups[3].Value, out var z)
        )
            throw new ArgumentException("Invalid float");
        position = new Vector3(x, y, z);

        if (match.Groups[1].Success)
            switch (match.Groups[1].Value.ToLower().Trim('/', '\\'))
            {
                case "cupboard":
                case "closet":
                    parentTo = GameObject.Find("Environment/HangarShip/StorageCloset");
                    if (parentTo == null)
                        throw new Exception("Storage closet not found");
                    break;
                case "file":
                case "cabinet":
                case "cabinets":
                case "file_cabinet":
                case "file_cabinets":
                case "filecabinet":
                case "filecabinets":
                    parentTo = GameObject.Find("Environment/HangarShip/FileCabinet");
                    if (parentTo == null)
                        throw new Exception("File cabinet not found");
                    break;
                case "ship":
                    parentTo = null;
                    break;
                case "environment":
                case "none":
                    parentTo = GameObject.Find("Environment");
                    if (parentTo == null)
                        throw new Exception("Environment not found, what the actual fuck");
                    break;
                default:
                    parentTo = GameObject.Find(match.Groups[1].Value);
                    if (parentTo == null)
                        throw new ArgumentException("Invalid parent object");
                    break;
            }
    }

    public override string ToString()
    {
        string path = "";

        if (parentTo != null)
        {
            path = parentTo.name;
            Transform i = parentTo.transform.parent;

            while (i != null)
            {
                path = i.name + "/" + path;
                i = i.parent;
            }

            path += ":";
        }

        return path + $"{position.x},{position.y},{position.z}";
    }

    public Vector3 position;
    public GameObject? parentTo;
}
