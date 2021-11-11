using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NxSpriteInfo
{
    private int _x;
    private int _y;
    private Sprite _sprite;
    private int _referenceCount;

    private int _width;
    private int _height;

    public int x { get { return _x; } }
    public int y { get { return _y; } }

    public Sprite sprite
    {
        get { return _sprite; }
    }

    public NxSpriteInfo(int x, int y, Texture2D mainTexture, int startX, int startY, int width, int height)
    {
        _x = x;
        _y = y;
        _referenceCount = 0;

        _width = width;
        _height = height;

        _sprite = Sprite.Create(mainTexture, new Rect(startX, startY, width, height), Vector2.one / 2f);
    }

    public bool IsEmpty()
    {
        return _referenceCount == 0;
    }

    public void AddReference()
    {
        ++_referenceCount;
        Debug.Log(string.Format("[AddReference]Sprite:[{0},{1}] ref:{2}", x, y, _referenceCount));
    }

    public void RemoveReference()
    {
        if (_referenceCount == 0) return;
        --_referenceCount;

        Debug.Log(string.Format("[RemoveReference]Sprite:[{0},{1}] ref:{2}", x, y, _referenceCount));
    }
}

public class DynamicAtlas : MonoBehaviour
{
    private const int MAX_DYNAMIC_ATLAS_SIZE = 1024;
    private const int DYNAMIC_ATLAS_CELL_SIZE = 128;
    private const int DYNAMIC_ATLAS_CELL_COUNT = MAX_DYNAMIC_ATLAS_SIZE / DYNAMIC_ATLAS_CELL_SIZE;

    [SerializeField]
    private Texture2D _dynamicAtlasTex;

    // 策略 分成格子
    private List<NxSpriteInfo> _spriteCacheList;
    private Dictionary<int, int> _spriteRedirectMap = new Dictionary<int, int>();

    private void Awake()
    {
        _dynamicAtlasTex = new Texture2D(MAX_DYNAMIC_ATLAS_SIZE, MAX_DYNAMIC_ATLAS_SIZE, TextureFormat.RGBA32, false);
        _initCacheSprite();

        //GetOrLoadSprite
        foreach (var item in transform.GetComponentsInChildren<Image>())
        {
            GetOrLoadSprite(item.sprite);
        }
    }

    private void _initCacheSprite()
    {
        int cellCount = DYNAMIC_ATLAS_CELL_COUNT;

        _spriteCacheList = new List<NxSpriteInfo>();
        for (int i = 0; i < cellCount; ++i)
        {
            for (int j = 0; j < cellCount; ++j)
            {
                _spriteCacheList.Add(new NxSpriteInfo(i, j,
                    _dynamicAtlasTex,
                    i * DYNAMIC_ATLAS_CELL_SIZE, j * DYNAMIC_ATLAS_CELL_SIZE,
                    DYNAMIC_ATLAS_CELL_SIZE, DYNAMIC_ATLAS_CELL_SIZE));
            }
        }
    }

    public Sprite GetOrLoadSprite(Sprite sprite)
    {
        // 拿缓存
        var spriteInstanceID = sprite.GetInstanceID();
        //Debug.Log(string.Format(" name: {0} instanceid: {1}", sprite.name, spriteInstanceID));
        int index = -1;
        if (_spriteRedirectMap.TryGetValue(spriteInstanceID, out index))
        {
            var newSprite = _spriteCacheList[index];
            newSprite.AddReference();
            return newSprite.sprite;
        }

        // 检查是不是本身就是动态生成的 如果是的话 什么都不用做
        for (int i = 0; i < _spriteCacheList.Count; ++i)
        {
            var sp = _spriteCacheList[i];
            if (sp.sprite == sprite)
            {
                return sprite;
            }
        }

        // 拿不到缓存就找个空格子新增
        var emptySprite = GetEmptySprite();
        if (emptySprite != null)
        {
            // GPU上直接操作 速度快 兼容性差
            Graphics.CopyTexture(sprite.texture, 0, 0, (int)sprite.rect.x, (int)sprite.rect.y, (int)sprite.rect.width, (int)sprite.rect.height,
                                _dynamicAtlasTex, 0, 0, (int)emptySprite.sprite.rect.x, (int)emptySprite.sprite.rect.y);

            // 这里要先删除上一个的
            index = GetIndex(emptySprite);
            foreach (var redirect in _spriteRedirectMap)
            {
                if (redirect.Value == index)
                {
                    _spriteRedirectMap.Remove(redirect.Key);
                    break;
                }
            }
            _spriteRedirectMap.Add(spriteInstanceID, GetIndex(emptySprite));
            emptySprite.AddReference();
            emptySprite.sprite.name = sprite.name + "(Dynamic)";
            return emptySprite.sprite;
        }

        // 找不到空格子就直接返回sprite
        return sprite;
    }

    public void ReleaseSprite(Sprite sprite)
    {
        for (int i = 0; i < _spriteCacheList.Count; ++i)
        {
            var sp = _spriteCacheList[i];
            if (sp.sprite == sprite)
            {
                sp.RemoveReference();
                break;
            }
        }
    }

    private NxSpriteInfo GetEmptySprite()
    {
        for (int i = 0; i < _spriteCacheList.Count; ++i)
        {
            var sp = _spriteCacheList[i];
            if (sp.IsEmpty())
                return sp;
        }
        return null;
    }

    private int GetIndex(NxSpriteInfo sprite)
    {
        return sprite.x * DYNAMIC_ATLAS_CELL_COUNT + sprite.y;
    }

}