using System;
using Lua;

namespace LethalShipSort.LuaObjects;

[LuaObject]
public partial class RaycastPos(
    Vector3 _position,
    Vector3? _direction = null,
    SortAPI.TRANSFORM _relative_to = default
)
{
    private SortAPI.TRANSFORM relative_to = _relative_to;
    private Vector3 position = _position;
    private Vector3 direction = _direction ?? new Vector3(UnityEngine.Vector3.down);

    [LuaMember(nameof(relative_to))]
    private int _RelativeTo
    {
        get => (int)relative_to;
        set =>
            relative_to = Enum.IsDefined(typeof(SortAPI.TRANSFORM), value)
                ? (SortAPI.TRANSFORM)value
                : throw new ArgumentOutOfRangeException();
    }

    public SortAPI.TRANSFORM RelativeTo
    {
        get => relative_to;
        set => relative_to = value;
    }

    [LuaMember(nameof(position))]
    public Vector3 Position
    {
        get => position;
        set => position = value;
    }

    [LuaMember(nameof(direction))]
    public Vector3 Direction
    {
        get => direction;
        set => direction = value;
    }

    [LuaMetamethod(LuaObjectMetamethod.Call)]
    public static RaycastPos New(RaycastPos _, Vector3 position, Vector3 direction, int relative_to)
    {
        if (!Enum.IsDefined(typeof(SortAPI.TRANSFORM), relative_to))
            throw new ArgumentOutOfRangeException();
        return new RaycastPos(position, direction, (SortAPI.TRANSFORM)relative_to);
    }

    [LuaMetamethod(LuaObjectMetamethod.ToString)]
    public override string ToString()
    {
        return $"{position} rel {relative_to} towards {direction}";
    }
}
