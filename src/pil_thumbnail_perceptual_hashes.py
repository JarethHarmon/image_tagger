import sys, io, pywt, math
import numpy as np
from PIL import Image, ImageFile, ImageOps
ImageFile.LOAD_TRUNCATED_IMAGES = True

ONE = np.uint64(1)  
ZERO = np.uint64(0)
EIGHT = np.uint64(8)	
SIZE = 8

def initialize(sv_path):
    image = Image.open(sv_path)
    image = ImageOps.exif_transpose(image)
    imageL = image.convert('L')
    
    avg_hash = calc_average_hash(imageL)
    wav_hash = calc_wavelet_hash(image)
    dif_hash = calc_dhash(imageL)
    colors = calc_color_buckets(image)
    return f'{avg_hash}?{wav_hash}?{dif_hash}!{colors}'

def save_webp_thumbnail(im_path, sv_path, sv_size):
    image = Image.open(im_path)
    image.thumbnail((sv_size, sv_size))
    image.save(sv_path, 'webp')
    image = ImageOps.exif_transpose(image)
    imageL = image.convert('L')

    avg_hash = calc_average_hash(imageL)
    wav_hash = calc_wavelet_hash(imageL)
    dif_hash = calc_dhash(imageL)
    colors = calc_color_buckets(image)
    return f'{avg_hash}?{wav_hash}?{dif_hash}!{colors}'

def convert_binary_to_ulong(arr):
    result = ZERO
    for i in range(0, len(arr)-1):
        if arr[i]: result |= ONE
        result <<= ONE
    return result

def calc_average_hash(imageL):
    pixels = np.asarray(imageL.resize((SIZE, SIZE), Image.Resampling.LANCZOS))
    avg = np.mean(pixels)

    diff = pixels > avg
    flat = diff.flatten()
    return convert_binary_to_ulong(flat)

def calc_wavelet_hash(imageL):
    natural_scale = 2 ** int(np.log2(min(imageL.size)))
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

def calc_dhash(imageL):
    pixels = np.asarray(imageL.resize((SIZE+1, SIZE), Image.Resampling.LANCZOS))
    diff = pixels[:, 1:] > pixels[:, :-1]
    flat = diff.flatten()
    return convert_binary_to_ulong(flat)

def calc_dhash_vertical(imageL):
    pixels = np.asarray(imageL.resize((SIZE, SIZE+1), Image.Resampling.LANCZOS))
    diff = pixels[1:, :] > pixels[:-1, :]
    flat = diff.flatten()
    return convert_binary_to_ulong(flat)

def calc_color_buckets(image):
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

    avg = np.add(np.sum(pixels, axis=2), -1 * a) // 3
    aa = (a > 127)
    light = (np.logical_and((avg > 195), aa)).sum() // divisor
    dark = (np.logical_and((avg < 64), aa)).sum() // divisor
    
    return f'{red}?{green}?{blue}?{alpha}?{light}?{dark}'
