import sys, io, pywt, math
import numpy as np
from PIL import Image, ImageFile, ImageOps
ImageFile.LOAD_TRUNCATED_IMAGES = True

ONE = np.uint64(1)  
ZERO = np.uint64(0)
EIGHT = np.uint64(8)	
SIZE = 8

# note that for any intensive operation that is performed multiple times, I should really do all of those in 
#   initialize / save_webp_thumbnail and pass them as arguments to the relevant functions

def initialize(im_path, sv_path):
    num_frames = getattr(Image.open(im_path), "n_frames", 1)
    image = Image.open(sv_path)
    imageL = image.convert('L')
    
    avg_hash = calc_average_hash(imageL)
    wav_hash = calc_wavelet_hash(imageL)
    dif_hash = calc_dhash(imageL)
    per_hash = calc_phash(imageL)
    colors = calc_color_buckets(image, imageL)
    
    return f'{num_frames}!{avg_hash}?{wav_hash}?{dif_hash}?{per_hash}!{colors}'

def save_webp_thumbnail(im_path, sv_path, sv_size):
    image = Image.open(im_path)
    num_frames = getattr(image, "n_frames", 1)
    image.thumbnail((sv_size, sv_size))
    image = ImageOps.exif_transpose(image)
    image.save(sv_path, 'webp')
    imageL = image.convert('L')

    avg_hash = calc_average_hash(imageL)
    wav_hash = calc_wavelet_hash(imageL)
    dif_hash = calc_dhash(imageL)
    per_hash = calc_phash(imageL)
    colors = calc_color_buckets(image, imageL)
    
    return f'{num_frames}!{avg_hash}?{wav_hash}?{dif_hash}?{per_hash}!{colors}'

def convert_binary_to_ulong(arr):
    result = ZERO
    MAX = len(arr) - 1
    for i,v in enumerate(arr[:-1]):
        if v: result |= ONE
        result <<= ONE
    if arr[MAX]: result |= ONE
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

def calc_phash(imageL):
    import scipy.fftpack
    size = 32
    pixels = np.asarray(imageL.resize((size, size), Image.Resampling.LANCZOS))
    dct = scipy.fftpack.dct(scipy.fftpack.dct(pixels, axis=0), axis=1)
    dctlowfreq = dct[:8, :8]
    median = np.median(dctlowfreq)
    diff = dctlowfreq > median
    flat = diff.flatten()
    return convert_binary_to_ulong(flat)

def calc_phash_simple(imageL):
    import scipy.fftpack
    size = 32
    pixels = np.asarray(imageL.resize((size, size), Image.Resampling.LANCZOS))
    dct = scipy.fftpack.dct(pixels)
    dctlowfreq = dct[:8, 1:8 + 1]
    avg = dctlowfreq.mean()
    diff = dctlowfreq > avg
    flat = diff.flatten()
    return convert_binary_to_ulong(flat)

def calc_color_buckets(image, imageL):
    a = np.asarray(image.convert('RGBA'))[:,:,3]
    aa = (a > 16)
    image_dimensions = image.width * image.height
    divisor = image_dimensions / 255
    alpha = int((a < 255).sum() // divisor)

    pixels = np.asarray(image.convert('HSV'))
    h = pixels[:,:,0]
    s = pixels[:,:,1]
    v = pixels[:,:,2] 
    
    ss = s >= 12
    vv = v >= 12
    sva = np.logical_and(np.logical_and(ss, vv), aa)
    
    rr = np.logical_and(np.logical_or(h < 21, h > 234), sva).sum()
    gg = np.logical_and(np.logical_and(h > 63, h < 106), sva).sum()
    bb = np.logical_and(np.logical_and(h > 149, h < 191), sva).sum()
    yy = np.logical_and(np.logical_and(h >= 21, h <= 63), sva).sum()
    cc = np.logical_and(np.logical_and(h >= 106, h <= 149), sva).sum()
    ff = np.logical_and(np.logical_and(h >= 191, h <= 234), sva).sum()
    hue_normalizer = image_dimensions / max(1, (rr + gg + bb + yy + cc + ff))

    red = int(hue_normalizer * rr // divisor)
    green = int(hue_normalizer * gg // divisor)
    blue = int(hue_normalizer * bb // divisor)
    yellow = int(hue_normalizer * yy // divisor)
    cyan = int(hue_normalizer * cc // divisor)
    fuchsia  = int(hue_normalizer * ff // divisor)

    vd = np.logical_and(aa, s > 67).sum()
    nn = np.logical_and(aa, np.logical_and(s <= 67, s >= 34)).sum()
    dd = np.logical_and(aa, s < 34).sum()
    sat_normalizer = image_dimensions / max(1, (vd + nn + dd))
    
    vivid = int(sat_normalizer * vd // divisor)
    neutral = int(sat_normalizer * nn // divisor)
    dull = int(sat_normalizer * dd // divisor)

    val = np.asarray(imageL.convert('HSV'))[:,:,2]
    ll = np.logical_and(aa, val > 67).sum()
    mm = np.logical_and(aa, np.logical_and(val <= 67, val >= 33)).sum()
    kk = np.logical_and(aa, val < 33).sum()
    val_normalizer = image_dimensions / max(1, (ll + mm + kk))
    
    light = int(val_normalizer * ll // divisor)
    medium = int(val_normalizer * mm // divisor)
    dark = int(val_normalizer * kk // divisor)

    return f'{red}?{green}?{blue}?{yellow}?{cyan}?{fuchsia}?{vivid}?{neutral}?{dull}?{light}?{medium}?{dark}?{alpha}'

