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
            @"(?:([\w/\\]+):)?(?:([\d-.]+),){2}([\d-.]+)(?::([A-Z]+))?$",
            RegexOptions.Multiline
        ).Match(s);
        if (!match.Success)
        {
            var flagmatch = new Regex("([A-Z]+)$", RegexOptions.Multiline).Match(s);
            if (!flagmatch.Success)
                throw new ArgumentException($"Invalid format ({s})");

            StringBuilder flog = new($">> ItemPosition init flags \"{s}\"");
            flags = new Flags(flagmatch.Groups[1].Value);
            LethalShipSort.Logger.LogDebug(flog.ToString());
            return;
        }

        StringBuilder log = new($">> ItemPosition init \"{s}\"");

        var xstr = match.Groups[2].Captures[0].Value;
        var ystr = match.Groups[2].Captures[1].Value;
        var zstr = match.Groups[3].Value;
        if (
            !float.TryParse(xstr, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(ystr, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(zstr, NumberStyles.Float, CultureInfo.InvariantCulture, out var z)
        )
            throw new ArgumentException(
                $"Invalid float ({s}) [\"{xstr}\", \"{ystr}\", \"{zstr}\"]"
            );
        position = new Vector3(x, y, z);

        flags = new Flags(match.Groups[4].Value);
        log.Append($"\n   flags are {flags}");

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

        return path
            + (
                position != null
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2}",
                        position.Value.x,
                        position.Value.y,
                        position.Value.z
                    )
                    : string.Empty
            )
            + (flags.ToString().Length > 0 ? ':' : string.Empty)
            + flags;
    }

    public struct Flags
    {
        public Flags(string s)
        {
            foreach (var flag in s)
                switch (flag)
                {
                    case NO_AUTO_SORT:
                        NoAutoSort = true;
                        break;
                    case KEEP_ON_CRUISER:
                        KeepOnCruiser = true;
                        break;
                    case IGNORE:
                        Ignore = true;
                        break;
                    case EXACT:
                        Exact = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown flag ({flag})");
                }
        }

        public const char NO_AUTO_SORT = 'A';
        public bool NoAutoSort;

        public const char KEEP_ON_CRUISER = 'C';
        public bool KeepOnCruiser;

        public const char IGNORE = 'N';
        public bool Ignore;

        public const char EXACT = 'X';
        public bool Exact;

        public override string ToString() =>
            $"{(NoAutoSort ? NO_AUTO_SORT : string.Empty)}{(KeepOnCruiser ? KEEP_ON_CRUISER : string.Empty)}{(Ignore ? IGNORE : string.Empty)}{(Exact ? EXACT : string.Empty)}";
    }

    public Vector3? position;
    public GameObject? parentTo;
    public Flags flags;
}
