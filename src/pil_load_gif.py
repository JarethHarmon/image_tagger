import sys, io
from PIL import Image, ImageFile, GifImagePlugin
ImageFile.LOAD_TRUNCATED_IMAGES = True
GifImagePlugin.LOADING_STRATEGY = GifImagePlugin.LoadingStrategy.RGB_ALWAYS

import base64
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

