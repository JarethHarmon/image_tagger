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
        return sv_type
    except:
        return 'error'

# need to look into either returning a tuple, or storing the variables in temp vars and retrieving them 1 by 1
# should actually be possible to directly return an object (class) as well, though currently I only need an array of ints
def save_thumbnails(im_paths, sv_paths, sv_types, sv_size):
    result = ''
    for i in range(0, len(im_paths)):
        result += save_PIL_thumbnail(im_paths[i], sv_paths[i], sv_size, sv_types[i]) + '?'
    return result

#save_thumbnails(['W:/fubuki-confused.gif'], ['W:/fubuki-confused.thumb'], ['jpeg'], 256)