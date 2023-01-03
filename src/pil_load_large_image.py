import sys, io, base64, clr
clr.AddReference('Image Tagger')
from Importer import PythonInterop
import numpy as np
from PIL import Image, ImageFile
ImageFile.LOAD_TRUNCATED_IMAGES = True

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

def load_large_image(impath, imhash, num_y, num_x):
    im = Image.open(impath)
    is_png = has_transparency(im)
    im_type = 'png' if is_png else 'jpeg'
    if is_png: 
        if im.mode != 'RGBA': 
            im = im.convert('RGBA')
    elif im.mode != 'RGB': im = im.convert('RGB')
    npim = np.asarray(im)
	
    csharp = PythonInterop()
    csharp.Setup()
		
    M = npim.shape[0] // num_x
    N = npim.shape[1] // num_y
    
    for x in range(0, npim.shape[0], M):
        for y in range(0, npim.shape[1], N):
            if csharp.StopLoading(imhash): break
            bi = io.BytesIO()
            nim = Image.fromarray(npim[x:x+M, y:y+N])
            if is_png: nim.save(bi, 'png', compress_level=0)
            else: nim.save(bi, 'jpeg', quality=95)
            base64_str = str(base64.b64encode(bi.getvalue()))
            base64_str = base64_str.replace('\'', '')
            base64_str = base64_str[1:len(base64_str)]
            base64_str = f'{im_type}?{imhash}?{base64_str}'
            csharp.SendImageTile(base64_str)
