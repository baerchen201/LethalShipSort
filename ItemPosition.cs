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
        const int X = 2;
        const int XOFFSET = 3;
        const int Y = 4;
        const int YOFFSET = 5;
        const int Z = 6;
        const int ZOFFSET = 7;
        const int R = 8;
        const int ROFFSET = 9;
        const int OFFSET = 10;
        const int FLAGS = 11;

        var match = new Regex(
            @"(?:([\w/\\]+):)?(?:(-?\d+(?:\.\d+)?)([+-]\d+(?:\.\d+)?)?,)(?:(-?\d+(?:\.\d+)?)([+-]\d+(?:\.\d+)?)?,)(-?\d+(?:\.\d+)?)([+-]\d+(?:\.\d+)?)?(?:,(\d+)([+-]\d+)?)?(?:,(\d+(?:\.\d+)?))?(?::([A-Z]+))?$",
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

        var xstr = match.Groups[X].Value;
        var ystr = match.Groups[Y].Value;
        var zstr = match.Groups[Z].Value;
        if (!float.TryParse(xstr, NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
            throw new ArgumentException($"Invalid {nameof(x)} value ({xstr})");
        if (!float.TryParse(ystr, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            throw new ArgumentException($"Invalid {nameof(y)} value ({ystr})");
        if (!float.TryParse(zstr, NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            throw new ArgumentException($"Invalid {nameof(z)} value ({zstr})");
        position = new Vector3(x, y, z);
        log.Append($"\n   position is {position}");

        var xoffsetstr = match.Groups[XOFFSET].Value;
        var yoffsetstr = match.Groups[YOFFSET].Value;
        var zoffsetstr = match.Groups[ZOFFSET].Value;
        float? xoffset = null,
            yoffset = null,
            zoffset = null;
        if (match.Groups[XOFFSET].Success)
            if (
                !float.TryParse(
                    xoffsetstr,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var _xoffset
                )
            )
                throw new ArgumentException($"Invalid {nameof(xoffset)} value ({xoffsetstr})");
            else
                xoffset = _xoffset;
        if (match.Groups[YOFFSET].Success)
            if (
                !float.TryParse(
                    yoffsetstr,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var _yoffset
                )
            )
                throw new ArgumentException($"Invalid {nameof(yoffset)} value ({ystr})");
            else
                yoffset = _yoffset;
        if (match.Groups[ZOFFSET].Success)
            if (
                !float.TryParse(
                    zoffsetstr,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var _zoffset
                )
            )
                throw new ArgumentException($"Invalid {nameof(zoffset)} value ({zstr})");
            else
                zoffset = _zoffset;
        if (xoffset != null || yoffset != null || zoffset != null)
        {
            positionOffset = new Vector3(xoffset ?? 0f, yoffset ?? 0f, zoffset ?? 0f);
            log.Append($"\n   fixed positional offset is {positionOffset}");
        }
        else
            log.Append($"\n   fixed positional offset is not specified");

        var rstr = match.Groups[R].Value;
        if (match.Groups[R].Success)
        {
            if (!int.TryParse(rstr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
                throw new ArgumentException($"Invalid {nameof(floorYRot)} value ({rstr})");
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
                throw new ArgumentException($"Invalid {nameof(roffset)} value ({roffsetstr})");
            rotationOffset = roffset == 0 ? null : roffset;
            log.Append($"\n   rotational offset is {roffset}");
        }
        else
            log.Append("\n   rotational offset not specified");

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
                throw new ArgumentException($"Invalid {nameof(randomOffset)} value ({offsetstr})");
            randomOffset = offset;
            log.Append($"\n   random positional offset is {randomOffset}");
        }
        else
            log.Append("\n   random positional offset not specified");

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
    public Vector3? positionOffset;
    public GameObject? parentTo;
    public int? floorYRot;
    public int? rotationOffset;
    public float? randomOffset;
    public Flags flags;
}
