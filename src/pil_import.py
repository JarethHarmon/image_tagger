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
    colors = calc_color_buckets(image)
    
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
    colors = calc_color_buckets(image)
    
    return f'{num_frames}!{avg_hash}?{wav_hash}?{dif_hash}?{per_hash}!{colors}'

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

def calc_color_buckets(image):
    a = np.asarray(image.convert('RGBA'))[:,:,3]
    divisor = int((image.width * image.height) / 256)
    alpha = (a < 255).sum() // divisor
    aa = (a > 16)

    pixels = np.asarray(image.convert('HSV'))
    h = pixels[:,:,0]
    s = pixels[:,:,1]
    v = pixels[:,:,2] 
    
    ss = s >= 10
    vv = v >= 10
    sva = np.logical_and(np.logical_and(ss, vv), aa)
    
    rr = np.logical_and(np.logical_or(h < 21, h > 234), sva)
    gg = np.logical_and(np.logical_and(h > 63, h < 106), sva)
    bb = np.logical_and(np.logical_and(h > 149, h < 191), sva)

    yy = np.logical_and(np.logical_and(h >= 21, h <= 63), sva)
    cc = np.logical_and(np.logical_and(h >= 106, h <= 149), sva)
    ff = np.logical_and(np.logical_and(h >= 191, h <= 234), sva)
    
    red = rr.sum() // divisor
    green = gg.sum() // divisor
    blue = bb.sum() // divisor
    
    yellow = yy.sum() // divisor
    cyan = cc.sum() // divisor
    fuchsia  = ff.sum() // divisor

    saa = np.logical_and(aa, s < 33)
    
    light = np.logical_and(saa, v > 67).sum() // divisor
    medium = np.logical_and(aa, np.logical_and(v <= 67, v >= 33)).sum() // divisor
    dark = np.logical_and(aa, v < 33).sum() // divisor
    
    vivid = np.logical_and(aa, s > 67).sum() // divisor
    neutral = np.logical_and(aa, np.logical_and(s <= 67, s >= 33)).sum() // divisor
    dull = saa.sum() // divisor

    #light = np.logical_and(np.logical_and(v > 75, s < 25), aa).sum() // divisor
    #dark = np.logical_and(v < 25, aa).sum() // divisor
    
    #nn = np.logical_and(s >= 40, s < 70)
    #dd = np.logical_and(ss, s < 40)
    
    #vivid = np.logical_and(s >= 70, vv).sum() // divisor
    #neutral = np.logical_and(nn, vv).sum() // divisor
    #dull = np.logical_and(dd, vv).sum() // divisor
    return f'{red}?{green}?{blue}?{yellow}?{cyan}?{fuchsia}?{vivid}?{neutral}?{dull}?{light}?{medium}?{dark}?{alpha}'

#image = Image.open('W:/images/16176b646fd9c33f3444b6850ea67d3a.jpg')
#image.thumbnail((256, 256))
#image = ImageOps.exif_transpose(image)
#image.save('W:/test_1.png', 'png')
#print(calc_color_buckets(image))