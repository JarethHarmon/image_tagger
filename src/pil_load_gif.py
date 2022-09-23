import sys, io, base64, clr
clr.AddReference('Image Tagger')
from Importer import PythonInterop
from PIL import Image, ImageFile, GifImagePlugin
ImageFile.LOAD_TRUNCATED_IMAGES = True
GifImagePlugin.LOADING_STRATEGY = GifImagePlugin.LoadingStrategy.RGB_ALWAYS

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
    
    for i in range(0, frame_count):
        bi = io.BytesIO()
        im.seek(i)
        im.save(bi, 'jpeg', disposal=1)
        base64_str = str(base64.b64encode(bi.getvalue()))
        base64_str = base64_str.replace('\'', '')
        base64_str = base64_str[1:len(base64_str)]
        base64_str = im_hash + '?' + path + '?' + str(im.info['duration']) + '?' + base64_str
        csharp.SendFrame(base64_str)
