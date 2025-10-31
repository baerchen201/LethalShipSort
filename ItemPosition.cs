using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LethalShipSort;

public struct ItemPosition
{
    public ItemPosition(string s)
    {
        var match = new Regex(
            @"(?:([\w/\\]+):)?(?:([\d-.]+),){2}([\d-.]+)(?:,([\d-.]+))?$",
            RegexOptions.Multiline
        ).Match(s);
        if (!match.Success)
            throw new ArgumentException($"Invalid format ({s})");
        StringBuilder log = new($">> ItemPosition init \"{s}\"");

        var xstr = match.Groups[2].Captures[0].Value;
        var ystr = match.Groups[2].Captures[1].Value;
        var zstr = match.Groups[3].Value;
        var rstr = match.Groups[4].Value;
        if (
            !float.TryParse(xstr, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(ystr, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(zstr, NumberStyles.Float, CultureInfo.InvariantCulture, out var z)
        )
            throw new ArgumentException(
                $"Invalid float ({s}) [\"{xstr}\", \"{ystr}\", \"{zstr}\"]"
            );
        position = new Vector3(x, y, z);
        log.Append($"\n   position is {position}");
        if (match.Groups[4].Success)
        {
            if (!int.TryParse(rstr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
                throw new ArgumentException($"Invalid int ({s}) \"{rstr}\"");
            floorYRot = r;
            log.Append($"\n   rotation is {floorYRot}");
        }
        else
            log.Append($"\n   rotation not specified");

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

        return path
            + string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2}",
                position.x,
                position.y,
                position.z
            )
            + (
                floorYRot == null
                    ? string.Empty
                    : string.Format(CultureInfo.InvariantCulture, ",{0}", floorYRot)
            );
    }

    public Vector3 position;
    public GameObject? parentTo;
    public int? floorYRot;
}
