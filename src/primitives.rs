use std::time::Duration;

use wgpu::{util::DeviceExt, BindGroup, BindGroupLayout, Device, Buffer};

// #[repr(C , align(16))]
// the paddings allow aliognment of 16bytes for my actual variables
#[repr(C)]
#[derive(Debug, Copy, Clone, bytemuck::Pod, bytemuck::Zeroable, Default)]
pub struct SDFPrimitive {
    position: [f32; 3],
    _pad1: f32,
    rotation: [f32; 4],
    data: [f32; 4],
    instances: [u32; 3],
    _pad2: f32,
    rgba: [f32; 4],
    typus: u32,
    _pad3: [f32; 3],
    // operation: u32,
    // blend_strength: f32,
    // filler: [u32; 5], // 32 byte alignment
}
// struct Primitive {
//     position: vec3<f32>,
//     rotation: vec4<f32>,
//     data: vec4<f32>,
//     instances: vec3<u32>,
//     rgba: vec4<f32>,
//     typus: u32,
//     // operation: u32,
//     // blend_strength: f32,
// }

impl SDFPrimitive {
    pub fn new() -> Self {
        Self {
            rgba: [1.0; 4],
            typus: 0,
            position: [0.0; 3],
            rotation: [0.0, 0.0, 0.0, 1.0],
            data: [0.1; 4],
            instances: [1; 3],
            ..Default::default()
        }
    }
}

pub struct PrimitiveManager {
    pub primitives: Vec<SDFPrimitive>,
    pub buffer: Buffer,
    pub bind_group: BindGroup,
    pub bind_group_layout: BindGroupLayout,
}

impl PrimitiveManager {
    pub fn new(device: &Device, primitive_count: usize) -> Self {
        let (bind_group, bind_group_layout, buffer) = mk_primitive_bind_group(device, primitive_count);
        
        let primitives = vec![SDFPrimitive::new();primitive_count];
        
        Self {
            primitives: primitives,
            buffer,
            bind_group,
            bind_group_layout,
        }
    }
    pub fn update_primitives<F>(&mut self, primitive_updater : F, queue: &wgpu::Queue) where F : Fn(& mut Vec<SDFPrimitive>) {
        primitive_updater(&mut self.primitives);
        queue.write_buffer(&self.buffer, 0, bytemuck::cast_slice(&self.primitives));
    }
    pub fn update(&mut self, dt: Duration, queue: &wgpu::Queue) {
        let updater = |primitives: &mut Vec<SDFPrimitive>| {
            for primitive in primitives.iter_mut() {
                // primitive.position[0] += 0.01*dt.as_secs_f32();
                primitive.data[0] += 0.002*dt.as_secs_f32();
                primitive.data[1] += 0.001*dt.as_secs_f32();
                primitive.data[2] += 0.001*dt.as_secs_f32();
                primitive.data[3] = 0.002;//*dt.as_secs_f32();
                // primitive.rotation[0] += 0.02*dt.as_secs_f32();
                primitive.rgba[0] -= 0.01*dt.as_secs_f32();
            }
        };
        self.update_primitives(updater, queue)
    }
}

fn mk_primitive_bind_group(device: &Device, primitive_count: usize) -> (BindGroup, BindGroupLayout, Buffer) {
    let buffer = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
        label: Some("Primitives Buffer"),
        contents: bytemuck::cast_slice(&vec![SDFPrimitive::new();primitive_count]),
        usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_DST,
    });

    let layout = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
        entries: &[wgpu::BindGroupLayoutEntry {
            binding: 0,
            visibility: wgpu::ShaderStages::VERTEX_FRAGMENT,
            ty: wgpu::BindingType::Buffer {
                ty: wgpu::BufferBindingType::Storage { read_only: true },
                has_dynamic_offset: false,
                min_binding_size: None,
            },
            count: None,

        }],
        label: Some("primitives_bind_group_layout"),
    });

    let bind_group = device.create_bind_group(&wgpu::BindGroupDescriptor {
        layout: &layout,
        entries: &[wgpu::BindGroupEntry {
            binding: 0,
            resource: buffer.as_entire_binding(),
        }],
        label: Some("primitives_bind_group"),
    });
    (bind_group, layout, buffer)
}
