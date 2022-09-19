import sys
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

def save_PIL_thumbnail(im_path, sv_path, sv_size, sv_type):
    try:
        im = Image.open(im_path)
        if sv_type == 'jpeg': 
            if has_transparency(im): 
                if im.mode != 'RGBA': im.convert('RGBA')
                sv_type = 'png'
            elif im.mode != 'RGB': im = im.convert('RGB')       
        im.thumbnail((sv_size, sv_size))
        im.save(sv_path, sv_type)
        
        if sv_type == 'png': return 1
        return 0
    except:
        return -1


def save_thumbnail(im_path, sv_path, sv_type, sv_size):
    result = 7
    result = save_PIL_thumbnail(im_path, sv_path, sv_size, sv_type)
    return result
