using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LethalShipSort;

public struct ItemPosition
{
    public ItemPosition(string s)
    {
        var match = new Regex(
            @"(?:([\w/\\]+):)?(?:([\d-.]+),){2}([\d-.]+)$",
            RegexOptions.Multiline
        ).Match(s);
        if (!match.Success)
            throw new ArgumentException($"Invalid format ({s})");
        StringBuilder log = new($">> ItemPosition init \"{s}\"");

        var xstr = match.Groups[2].Captures[0].Value;
        var ystr = match.Groups[2].Captures[1].Value;
        var zstr = match.Groups[3].Value;
        if (
            !float.TryParse(xstr, out var x)
            || !float.TryParse(ystr, out var y)
            || !float.TryParse(zstr, out var z)
        )
            throw new ArgumentException(
                $"Invalid float ({s}) [\"{xstr}\", \"{ystr}\", \"{zstr}\"]"
            );
        position = new Vector3(x, y, z);
        log.Append($"\n   position is {position}");

        if (match.Groups[1].Success)
            switch (match.Groups[1].Value.ToLower().Trim('/', '\\'))
            {
                case "cupboard":
                case "closet":
                    log.Append("\n   parent object is closet");
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
                    log.Append("\n   parent object is file cabinet");
                    parentTo = GameObject.Find("Environment/HangarShip/FileCabinet");
                    if (parentTo == null)
                        throw new Exception("File cabinet not found");
                    break;
                case "ship":
                    log.Append("\n   parent object is ship");
                    parentTo = null;
                    break;
                case "environment":
                case "none":
                    log.Append("\n   parent object is none (environment)");
                    parentTo = GameObject.Find("Environment");
                    if (parentTo == null)
                        throw new Exception("Environment not found, what the actual fuck");
                    break;
                default:
                    log.Append($"\n   parent object is custom ({match.Groups[1].Value}) ");
                    parentTo = GameObject.Find(match.Groups[1].Value);
                    if (parentTo == null)
                        throw new ArgumentException("Invalid parent object");
                    log.Append(parentTo.ToString());
                    break;
            }
        else
            log.Append("\n   parent object not specified");

        LethalShipSort.Logger.LogDebug(log.ToString());
    }

    public override string ToString()
    {
        var path = "";

        if (parentTo != null)
        {
            path = parentTo.name;
            var i = parentTo.transform.parent;

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
