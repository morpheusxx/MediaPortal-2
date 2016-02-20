float g_offsetX;
float g_offsetY;
float g_angle;
float g_alpha;
float4 color;

float4 PixelEffect(in float2 texcoord, in sampler TextureSampler, in float4 framedata) : COLOR
{
  //Source color
  float4 ret = tex2D(TextureSampler, texcoord);

  //Determine shadow pixel
  float2 pixel;
  float2 pt;
  if (g_angle)
  {
    float theta = g_angle / 180.0*3.154159;
    pt = float2(cos(theta) * g_offsetX - sin(theta) * g_offsetY, sin(theta) * g_offsetX + cos(theta) * g_offsetY);
    pixel = texcoord - float2(pt.x * framedata.x, pt.y * framedata.y);
  }
  //No angle, skip some calculations
  else
  {
    pixel = texcoord - float2(g_offsetX * framedata.x, g_offsetY * framedata.y);
  }

  //Exit if no shadow
  if (pixel.x < 0 || pixel.x>1 || pixel.y < 0 || pixel.y>1)
  {
  }
  else
  {
    float4 shadow = color;
      shadow.a = tex2D(TextureSampler, pixel).a * g_alpha;
    //Thank, you Wikipedia. Thanks. *sniffs*
    float new_a = 1 - (1 - ret.a) * (1 - shadow.a);
    ret.rgb = (ret.rgb * ret.a + shadow.rgb * shadow.a * (1 - ret.a)) / new_a;
    ret.a = new_a;
  }
  return ret;
}
