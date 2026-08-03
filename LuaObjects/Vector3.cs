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
}
