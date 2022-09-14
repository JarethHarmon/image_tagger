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

if len(sys.argv) > 4:
    im_paths = sys.argv[1].split('?')           # path the full image is located at
    sv_paths = sys.argv[2].split('?')           # path the thumbnail will be saved at
    sv_size = int(sys.argv[3])                  # max dimensions of the thumbnail
    sv_types = sys.argv[4].split('?')           # the type ('jpeg' or 'png') that the thumbnail will be saved as
    
    result = ''
    for i in range(0, len(im_paths)):
        result += save_PIL_thumbnail(im_paths[i], sv_paths[i], sv_size, sv_types[i]) + '?'
    print(result)


