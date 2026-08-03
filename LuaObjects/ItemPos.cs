using System;
using Lua;

namespace LethalShipSort.LuaObjects;

[LuaObject]
public partial class ItemPos(
    Vector3 _position,
    int _rotation = 0,
    SortAPI.PARENT _parent_to = default,
    SortAPI.RELATIVE _relative_to = default,
    SortAPI.ROTATE _rotation_mode = default
)
{
    private SortAPI.PARENT parent_to = _parent_to;
    private SortAPI.RELATIVE relative_to = _relative_to;
    private Vector3 position = _position;

    private int rotation = _rotation;
    private SortAPI.ROTATE rotation_mode = _rotation_mode;

    [LuaMember(nameof(parent_to))]
    private int _ParentTo
    {
        get => (int)parent_to;
        set =>
            parent_to = Enum.IsDefined(typeof(SortAPI.PARENT), value)
                ? (SortAPI.PARENT)value
                : throw new ArgumentOutOfRangeException();
    }

    [LuaMember(nameof(relative_to))]
    private int _RelativeTo
    {
        get => (int)relative_to;
        set =>
            relative_to = Enum.IsDefined(typeof(SortAPI.RELATIVE), value)
                ? (SortAPI.RELATIVE)value
                : throw new ArgumentOutOfRangeException();
    }

    public SortAPI.PARENT ParentTo
    {
        get => parent_to;
        set => parent_to = value;
    }

    public SortAPI.RELATIVE RelativeTo
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

    [LuaMember(nameof(rotation))]
    public int Rotation
    {
        get => rotation;
        set => rotation = value % 360;
    }

    [LuaMember(nameof(rotation_mode))]
    private int _RotationMode
    {
        get => (int)rotation_mode;
        set =>
            rotation_mode = Enum.IsDefined(typeof(SortAPI.ROTATE), value)
                ? (SortAPI.ROTATE)value
                : throw new ArgumentOutOfRangeException();
    }

    public SortAPI.ROTATE RotationMode
    {
        get => rotation_mode;
        set => rotation_mode = value;
    }

    [LuaMetamethod(LuaObjectMetamethod.Call)]
    public static ItemPos New(ItemPos _, Vector3 position)
    {
        return new ItemPos(position);
    }

    [LuaMember(nameof(with_parent))]
    public ItemPos with_parent(int _parent_to)
    {
        _ParentTo = _parent_to;
        return this;
    }

    [LuaMember(nameof(with_relative))]
    public ItemPos with_relative(int _relative_to)
    {
        _RelativeTo = _relative_to;
        return this;
    }

    [LuaMember(nameof(with_rotation))]
    public ItemPos with_rotation(int rotation)
    {
        Rotation = rotation;
        return this;
    }

    [LuaMember(nameof(with_rotation_mode))]
    public ItemPos with_rotation_mode(int _rotation_mode)
    {
        _RotationMode = _rotation_mode;
        return this;
    }

    [LuaMetamethod(LuaObjectMetamethod.ToString)]
    public override string ToString()
    {
        return $"{position} rel {relative_to} rot {rotation_mode switch { SortAPI.ROTATE.None => SortAPI.ROTATE.None, _ => $"{rotation_mode} deg {rotation}" }} parent {parent_to}";
    }
}
