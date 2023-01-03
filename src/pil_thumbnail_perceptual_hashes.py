import sys, io, pywt, math
import numpy as np
from PIL import Image, ImageFile, ImageOps
ImageFile.LOAD_TRUNCATED_IMAGES = True

ONE = np.uint64(1)  
ZERO = np.uint64(0)
EIGHT = np.uint64(8)	
SIZE = 8

red = 0
green = 0
blue = 0
alpha = 0
light = 0
dark = 0

image = None
imageL = None

def initialize(sv_path):
    global image, imageL # now that I know about global keyword, I could probably get rid of the clr stuff for animations
    image = Image.open(sv_path)
    image = ImageOps.exif_transpose(image)
    imageL = image.convert('L')

def save_webp_thumbnail(im_path, sv_path, sv_size):
    global image, imageL
    image = Image.open(im_path)
    image.thumbnail((sv_size, sv_size))
    image.save(sv_path, 'webp')
    image = ImageOps.exif_transpose(image)
    imageL = image.convert('L')

def convert_binary_to_ulong(arr):
    result = ZERO
    for i in range(0, len(arr)-1):
        if arr[i]: result |= ONE
        result <<= ONE
    return result

def calc_average_hash():
    pixels = np.asarray(imageL.resize((SIZE, SIZE), Image.Resampling.LANCZOS))
    avg = np.mean(pixels)

    diff = pixels > avg
    flat = diff.flatten()
    return convert_binary_to_ulong(flat)

def calc_wavelet_hash():
    natural_scale = 2 ** int(np.log2(min(image.size)))
    scale = max(natural_scale, SIZE)
    
    max_level = int(np.log2(scale))
    level = int(np.log2(SIZE)) # just 3 I think
    dwt_level = max_level - level
    
    pixels = np.float32(imageL.resize((scale, scale), Image.Resampling.LANCZOS)) / 255
    
    coeffs = pywt.wavedec2(pixels, 'haar', level=max_level)
    coeffs = list(coeffs)
    coeffs[0] *= 0
    pixels = pywt.waverec2(coeffs, 'haar')
    
    coeffs = pywt.wavedec2(pixels, 'haar', level=dwt_level)
    dwt_low = coeffs[0]
    median = np.median(dwt_low)
    
    diff = dwt_low > median
    flat = diff.flatten()
    return convert_binary_to_ulong(flat)

def calc_dhash():
    pixels = np.asarray(imageL.resize((SIZE+1, SIZE), Image.Resampling.LANCZOS))
    diff = pixels[:, 1:] > pixels[:, :-1]
    flat = diff.flatten()
    return convert_binary_to_ulong(flat)

def calc_dhash_vertical():
    pixels = np.asarray(imageL.resize((SIZE, SIZE+1), Image.Resampling.LANCZOS))
    diff = pixels[1:, :] > pixels[:-1, :]
    flat = diff.flatten()
    return convert_binary_to_ulong(flat)

def calc_color_buckets():
    global red, green, blue, alpha, light, dark
    pixels = np.asarray(image.convert('RGBA'))
    divisor = int(math.sqrt(image.width * image.height))
    
    r = pixels[:,:,0]
    g = pixels[:,:,1]
    b = pixels[:,:,2]
    a = pixels[:,:,3]
    
    rr = np.logical_or(np.logical_and(r > g, r >= b), np.logical_and(r >= g, r > b))
    gg = np.logical_or(np.logical_and(g > b, g >= r), np.logical_and(g >= b, g > r))
    bb = np.logical_or(np.logical_and(b > r, b >= g), np.logical_and(b >= r, b > g))

    red = rr.sum() // divisor
    green = gg.sum() // divisor
    blue = bb.sum() // divisor
    
    alpha = (a < 127).sum() // divisor
    # sums colors (including alpha) of each pixel, subtracts the alpha value from that, filters pixels to only ones with an alpha > 127
    #   repeat/reshape ensure the where clause has the same size and shape as the pixels array; finally divides everything by 3 to get 
    #   the average of each pixel; this is then used to determine the brightness of a color
    # this could be accomplished much easier by converting to grayscale, but image ops are slow compared to numpy
    # that said, I do already have an 'L' version of the image created, so I might be able to use that instead; would still need the alpha reshapes
    #   to ensure pixels with alpha < 127 are not counted as dark/light and are instead counted as alpha
    # could also do a weighted count for alpha, where I just add each alpha value together before dividing, this does not work for colors
    # because white pixels will increase r,g, and b, but it should not cause issues for alpha
    avg = np.add(-1 * a, np.sum(pixels, axis=2, where=((a > 127).repeat(4).reshape(image.height, image.width, 4)))) // 3 #reshape(256, 256, 4)))) // 3
    light = (avg > 195).sum() // divisor
    dark = (avg < 64).sum() // divisor
    return f'{red}?{green}?{blue}?{alpha}?{light}?{dark}'
    #return list((red, green, blue, alpha, light, dark))
