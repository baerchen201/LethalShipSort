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
        const int PARENT = 1;
        const int XY = 2;
        const int Z = 3;
        const int R = 4;
        const int ROFFSET = 5;
        const int OFFSET = 6;
        const int FLAGS = 7;

        var match = new Regex(
            @"(?:([\w/\\]+):)?(?:([\d-.]+),){2}([\d-.]+)(?:,([\d]+)([+-][\d]+)?)?(?:,([\d.]+))?(?::([A-Z]+))?$",
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

        var xstr = match.Groups[XY].Captures[0].Value;
        var ystr = match.Groups[XY].Captures[1].Value;
        var zstr = match.Groups[Z].Value;
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

        var rstr = match.Groups[R].Value;
        if (match.Groups[R].Success)
        {
            if (!int.TryParse(rstr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
                throw new ArgumentException($"Invalid integer ({s}) \"{rstr}\"");
            floorYRot = r;
            log.Append($"\n   rotation is {floorYRot}");
        }
        else
            log.Append("\n   rotation not specified");

        var roffsetstr = match.Groups[ROFFSET].Value;
        if (match.Groups[ROFFSET].Success)
        {
            if (
                !int.TryParse(
                    roffsetstr,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var roffset
                )
            )
                throw new ArgumentException($"Invalid integer ({s}) \"{roffsetstr}\"");
            rotationOffset = roffset == 0 ? null : roffset;
            log.Append($"\n   rotation offset is {roffset}");
        }
        else
            log.Append("\n   rotation offset not specified");

        var offsetstr = match.Groups[OFFSET].Value;
        if (match.Groups[OFFSET].Success)
        {
            if (
                !float.TryParse(
                    offsetstr,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var offset
                )
            )
                throw new ArgumentException($"Invalid float ({s}) \"{offsetstr}\"");
            randomOffset = offset;
            log.Append($"\n   random offset is {randomOffset}");
        }
        else
            log.Append("\n   random offset not specified");

        flags = new Flags(match.Groups[FLAGS].Value);
        log.Append($"\n   flags are {flags}");

        var parentstr = match.Groups[PARENT].Value;
        if (match.Groups[PARENT].Success)
            switch (parentstr.ToLower().Trim('/', '\\'))
            {
                case "cupboard":
                case "closet":
                case "storage":
                case "storagecloset":
                    log.Append("\n   parent object is closet");
                    parentTo = GameObject.Find("Environment/HangarShip/StorageCloset");
                    if (parentTo == null)
                        throw new Exception("Storage closet not found");
                    break;
                case "file":
                case "filecabinet":
                case "filecabinets":
                    log.Append("\n   parent object is file cabinet");
                    parentTo = GameObject.Find("Environment/HangarShip/FileCabinet");
                    if (parentTo == null)
                        throw new Exception("File cabinet not found");
                    break;
                case "bunkbed":
                case "bunkbeds":
                    log.Append("\n   parent object is bunkbeds");
                    parentTo = GameObject.Find("Environment/HangarShip/Bunkbeds");
                    if (parentTo == null)
                        throw new Exception("Bunkbeds not found");
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
                        throw new Exception("Environment not found");
                    break;
                default:
                    log.Append($"\n   parent object is custom ({parentstr}) ");
                    parentTo = GameObject.Find(parentstr);
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
            + (
                floorYRot == null
                    ? randomOffset == null
                        ? ",0"
                        : string.Empty
                    : string.Format(CultureInfo.InvariantCulture, ",{0}", floorYRot)
            )
            + (
                rotationOffset == null
                    ? string.Empty
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}{1}",
                        rotationOffset < 0 ? "-" : "+",
                        Math.Abs(rotationOffset.Value)
                    )
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
                    case PARENT:
                        Parent = true;
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

        public const char PARENT = 'P';
        public bool Parent;

        public const char EXACT = 'X';
        public bool Exact;

        public override string ToString() =>
            $"{(NoAutoSort ? NO_AUTO_SORT : string.Empty)}{(KeepOnCruiser ? KEEP_ON_CRUISER : string.Empty)}{(Ignore ? IGNORE : string.Empty)}{(Parent ? PARENT : string.Empty)}{(Exact ? EXACT : string.Empty)}";

        public Flags FilterPositionRelated() => this & new Flags { Parent = true, Exact = true };

        public Flags FilterFilteringRelated() =>
            this
            & new Flags
            {
                NoAutoSort = true,
                KeepOnCruiser = true,
                Ignore = true,
            };

        public static Flags operator |(Flags _this, Flags other) =>
            new()
            {
                NoAutoSort = _this.NoAutoSort || other.NoAutoSort,
                KeepOnCruiser = _this.KeepOnCruiser || other.KeepOnCruiser,
                Ignore = _this.Ignore || other.Ignore,
                Parent = _this.Parent || other.Parent,
                Exact = _this.Exact || other.Exact,
            };

        public static Flags operator &(Flags _this, Flags other) =>
            new()
            {
                NoAutoSort = _this.NoAutoSort && other.NoAutoSort,
                KeepOnCruiser = _this.KeepOnCruiser && other.KeepOnCruiser,
                Ignore = _this.Ignore && other.Ignore,
                Parent = _this.Parent && other.Parent,
                Exact = _this.Exact && other.Exact,
            };
    }

    public Vector3? position;
    public GameObject? parentTo;
    public int? floorYRot;
    public int? rotationOffset;
    public float? randomOffset;
    public Flags flags;
}
