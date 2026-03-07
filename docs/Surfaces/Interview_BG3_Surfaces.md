our surfaces uh so we have many gameplay surfaces
spawning in very noisy patterns so we can have uh blood water ice fire uh
poison many more uh and these are deferred decals with some effects spawn
on top these decals however they are rendered in quite unusual way this is
not a simple shape always as you can see with the water there um they can take
any form grow shrink take over another surface they can grow quite large as well so this is the gameplay data that
we get from our AI GD it just tells us for each cell where these surfaces are so what can we do with this for
rendering deals well two options we can R render many small decals one per tile
but smoothing getting smooth edges then is quite difficult unless we spawn slightly larger ones and just basically
splat them over each other but that's a lot of overdraw we could also render a larger
Deco using a texture mask but and that gives us smooth edges because when use
linear filtering uh but the surfaces can grow so do you then grow the decool or
we do we render multiple larger ones but then you get seams so this was kind of a difficult problem to
solve um so we thought like well even with the mosques the empty SP the empty
space turned out to be quite costly because for deferred decals you need to sample the de buffer convert it up to
World space then to AI grid space to then sample surface Mouse texture to then see that there is no surface and
then early out that's not really a good early out if you need to do two texture samples for all the empty areas so one
large decal per surface type was definitely a noo no large decal over the entire Terrain
so how did we do this we dynamically generate a mesh based on the surface mask but not in 3D instead we're
generating screen space tiles uh we use a fixed chair vertex buffers vertex
buffer of all the tiles the index buffer is then generated per surface type with a compute Shader for every pixel in a
tile we check what surfaces are there for each surface type within a tile we
then write indices if the surface is present or we write zero indices so they're not rized when there is no
surface of that type and this gives us the nice benefit that only pixels where the surface is actually visible are
causing these tiles to be generated so it's automatically well cold you could say when it's behind other objects and
this means that we don't even have to do the typical that stencil test any more uh depth or dep stencil test uh for
different decals and it looks a bit like this so as you can see there's a bit of
padding space for each surface this is because artists want to be able to distort the edges um and this is A View
From a bit bit further away so as you can see there's quite a lot of decals and as soon as gameplay starts there and
there there's combat for example things can change very wildly so this helped reduce uh performance overhead