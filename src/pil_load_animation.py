import sys, io, base64, clr
clr.AddReference('Image Tagger')
from Importer import PythonInterop
from PIL import Image, ImageFile, GifImagePlugin
ImageFile.LOAD_TRUNCATED_IMAGES = True
GifImagePlugin.LOADING_STRATEGY = GifImagePlugin.LoadingStrategy.RGB_ALWAYS

def has_transparency(img):
    if img.info.get("transparency", None) is not None:
        return True
    if img.mode == "P":
        transparent = img.info.get("transparency", -1)
        for _, index in img.getcolors():
            if index == transparent:
                return True
    elif img.mode == "RGBA":
        extrema = img.getextrema()
        if extrema[3][0] < 255:
            return True
    return False

def load_gif(path):
    im = Image.open(path)
    frames = []
    for i in range(0, im.n_frames):
        bi = io.BytesIO()
        im.seek(i)
        im.save(bi, 'jpeg', disposal=1)
        base64_str = str(base64.b64encode(bi.getvalue()))
        base64_str = base64_str.replace('\'', '')
        base64_str = base64_str[1:len(base64_str)]
        base64_str = str(im.info['duration']) + '?' + base64_str
        frames.append(base64_str)
    return frames

def get_gif_frames(path, im_hash):
    im = Image.open(path)
    frame_count = im.n_frames
    csharp = PythonInterop()
    csharp.Setup()
    csharp.SendFrameCount(frame_count)
    csharp.SetAnimatedImageType(False)
    
    for i in range(0, frame_count):
        if csharp.StopLoading(path): break
        bi = io.BytesIO()
        im.seek(i)
        im.save(bi, 'jpeg', disposal=1, quality=95)
        base64_str = str(base64.b64encode(bi.getvalue()))
        base64_str = base64_str.replace('\'', '')
        base64_str = base64_str[1:len(base64_str)]
        base64_str = im_hash + '?' + path + '?' + str(im.info['duration']) + '?' + base64_str
        csharp.SendFrame(base64_str)

def get_apng_frames(path, im_hash):
    im = Image.open(path)
    frame_count = im.n_frames
    csharp = PythonInterop()
    csharp.Setup()
    csharp.SendFrameCount(frame_count)
    is_png = has_transparency(im)
    csharp.SetAnimatedImageType(is_png)
    
    for i in range(0, frame_count):
        if csharp.StopLoading(path): break
        bi = io.BytesIO()
        im.seek(i)
        if is_png: im.save(bi, 'png', blend=1, compress_level=0) # 6 by default
        else: im.convert('RGB').save(bi, 'jpeg', blend=1, quality=95)
        base64_str = str(base64.b64encode(bi.getvalue()))
        base64_str = base64_str.replace('\'', '')
        base64_str = base64_str[1:len(base64_str)]
        base64_str = im_hash + '?' + path + '?' + str(im.info['duration']) + '?' + base64_str
        csharp.SendAFrame(base64_str)