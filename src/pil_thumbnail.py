import sys
from PIL import Image

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
    im = Image.open(im_path)
    # need to figure out all the types and whether I need to convert it to RGBA for saving as png (if not RGBA already)
    if sv_type == 'jpeg': 
        if has_transparency(im): sv_type = 'png'
        else: im = im.convert('RGB')       # only needed if saving as jpg
    im.thumbnail((sv_size, sv_size))
    im.save(sv_path, sv_type)
    print(sv_type)

if len(sys.argv) > 4:
    im_path = sys.argv[1]       # path the full image is located at
    sv_path = sys.argv[2]       # path the thumbnail will be saved at
    sv_size = int(sys.argv[3])  # max dimensions of the thumbnail
    sv_type = sys.argv[4]       # the type ('jpeg' or 'png') that the thumbnail will be saved as
    save_PIL_thumbnail(im_path, sv_path, sv_size, sv_type)


