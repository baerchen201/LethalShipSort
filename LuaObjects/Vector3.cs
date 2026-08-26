using Lua;

namespace LethalShipSort.LuaObjects;

[LuaObject]
public partial class Vector3(UnityEngine.Vector3 _vector3)
{
    private UnityEngine.Vector3 vector3 = _vector3;

    [LuaMember(nameof(vector3.x))]
    public float X
    {
        get => vector3.x;
        set => vector3 = vector3 with { x = value };
    }

    [LuaMember(nameof(vector3.y))]
    public float Y
    {
        get => vector3.y;
        set => vector3 = vector3 with { y = value };
    }

    [LuaMember(nameof(vector3.z))]
    public float Z
    {
        get => vector3.z;
        set => vector3 = vector3 with { z = value };
    }

    public static implicit operator UnityEngine.Vector3(Vector3 vector3)
    {
        return vector3.vector3;
    }

    [LuaMetamethod(LuaObjectMetamethod.Call)]
    public static Vector3 New(Vector3 _, float x, float y, float z)
    {
        return new Vector3(new UnityEngine.Vector3(x, y, z));
    }

    [LuaMetamethod(LuaObjectMetamethod.Add)]
    public static Vector3 Add(Vector3 a, Vector3 b)
    {
        return new Vector3(a.vector3 + b.vector3);
    }

    [LuaMetamethod(LuaObjectMetamethod.Sub)]
    public static Vector3 Sub(Vector3 a, Vector3 b)
    {
        return new Vector3(a.vector3 - b.vector3);
    }

    [LuaMetamethod(LuaObjectMetamethod.Mul)]
    public static Vector3 Mul(Vector3 a, float b)
    {
        return new Vector3(a.vector3 * b);
    }

    [LuaMetamethod(LuaObjectMetamethod.Div)]
    public static Vector3 Div(Vector3 a, float b)
    {
        return new Vector3(a.vector3 / b);
    }

    [LuaMetamethod(LuaObjectMetamethod.Unm)]
    public static Vector3 Unm(Vector3 a)
    {
        return new Vector3(-a.vector3);
    }

    [LuaMetamethod(LuaObjectMetamethod.Index)]
    public static float Index(Vector3 a, int i)
    {
        return a.vector3[i];
    }

    [LuaMember(nameof(vector3.normalized))]
    public Vector3 Normalized()
    {
        return new Vector3(vector3.normalized);
    }

    [LuaMetamethod(LuaObjectMetamethod.ToString)]
    public override string ToString()
    {
        return vector3.ToString();
    }

    [LuaMember(nameof(Equals))]
    public bool Equals(Vector3 b)
    {
        return vector3 == b.vector3;
    }

    public string? GetError()
    {
        if (!float.IsFinite(vector3.x))
            return $"{nameof(vector3.x)} is {vector3.x}";
        if (!float.IsFinite(vector3.y))
            return $"{nameof(vector3.y)} is {vector3.y}";
        if (!float.IsFinite(vector3.z))
            return $"{nameof(vector3.z)} is {vector3.z}";

        return null;
    }
}
