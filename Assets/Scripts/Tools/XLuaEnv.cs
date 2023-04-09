using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XLua;

public class XLuaEnv
{
    private static XLuaEnv _instance;

    public static XLuaEnv Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new XLuaEnv();
            }
            return _instance;
        }
    }

    private XLuaEnv()
    {
        _env = new LuaEnv();
        _env.AddLoader(LoadLuaEnv);
    }

    private LuaEnv _env;

    private byte[] LoadLuaEnv(ref string filePath)
    {
        string path = Application.dataPath;
        path = path.Replace("Assets", "LuaPath/");
        path = path + filePath + ".lua";
        if (!System.IO.File.Exists(path))
            return null;
        return System.IO.File.ReadAllBytes(path);
    }

    public object[] DoString(string code)
    {
        return _env.DoString(code);
    }

    public LuaTable Global
    {
        get
        {
            return _env.Global;
        }
    }

    public void Unload()
    {
        _env.Dispose();
        _instance = null;
    }
}
